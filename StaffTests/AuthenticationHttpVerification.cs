using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Student_Management_System.Controllers;
using Student_Management_System.Models;

// Runs the actual application entry point with its real cookie/CSRF/rate-limit
// middleware and the suite's disposable SQL database. No production test auth.
internal static class AuthenticationHttpVerification
{
    public static async Task RunAsync(string connection, ApplicationDbContext db, UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles, Action<bool, string> check)
    {
        const string email = "auth-http@test.invalid";
        var password = "HttpTest!" + Guid.NewGuid().ToString("N") + "1a";
        var user = new ApplicationUser { FullName = "HTTP auth fixture", UserName = email, Email = email, EmailConfirmed = true,
            PublicUserId = "AUTH-HTTP", PhoneNumber = "8999999996" };
        check((await users.CreateAsync(user, password)).Succeeded, "HTTP authentication fixture must be created.");
        if (!await roles.RoleExistsAsync("Employee")) await roles.CreateAsync(new IdentityRole("Employee"));
        await users.AddToRoleAsync(user, "Employee");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var output = Path.GetFullPath(Path.Combine(".build", "auth-http-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(output);
        var certificatePath = Path.Combine(output, "localhost.pfx");
        var keyDirectory = Path.Combine(output, "keys");
        var certificatePassword = Guid.NewGuid().ToString("N");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddIpAddress(IPAddress.Loopback); names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        await File.WriteAllBytesAsync(certificatePath, certificate.Export(X509ContentType.Pfx, certificatePassword));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { typeof(AccountController).Assembly.Location, "--environment", "Development", "--SkipDatabaseBootstrap=true", "--DatabaseBootstrap:ApplyMigrationsOnStartup=false", "--urls", $"https://127.0.0.1:{port}" }) start.ArgumentList.Add(argument);
        start.Environment["ConnectionStrings__DefaultConnection"] = connection;
        start.Environment["Smtp__Enabled"] = "false";
        start.Environment["SMTP_ENABLED"] = "false";
        start.Environment["Kestrel__Certificates__Default__Path"] = certificatePath;
        start.Environment["Kestrel__Certificates__Default__Password"] = certificatePassword;
        start.Environment["DataProtection__KeyDirectory"] = keyDirectory;
        start.Environment["Logging__EventLog__LogLevel__Default"] = "None";
        using var process = Process.Start(start) ?? throw new Exception("The isolated HTTP test host could not start.");
        var startupLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
        void Capture(string? line) { if (line != null && startupLog.Count < 120) startupLog.Enqueue(line); }
        process.OutputDataReceived += (_, args) => Capture(args.Data); process.ErrorDataReceived += (_, args) => Capture(args.Data);
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        try
        {
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false,
                ServerCertificateCustomValidationCallback = (message, presented, _, _) => message.RequestUri?.IsLoopback == true && presented?.Thumbprint == certificate.Thumbprint };
            using var client = new HttpClient(handler) { BaseAddress = new Uri($"https://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(15) };
            var ready = false;
            for (var attempt = 0; attempt < 75 && !process.HasExited; attempt++)
            {
                try { using var response = await client.GetAsync("/Account/Login"); ready = response.IsSuccessStatusCode; if (ready) break; }
                catch (HttpRequestException) { }
                await Task.Delay(200);
            }
            if (!ready) await File.WriteAllLinesAsync(Path.Combine(output, "host.log"), startupLog);
            check(ready, $"The real application must start over HTTPS against only the disposable database. Startup diagnostics: {Path.Combine(output, "host.log")}");
            check((await client.GetAsync("/api/lookups/classes?departmentId=1")).StatusCode == HttpStatusCode.Unauthorized,
                "Anonymous API requests must return 401 instead of redirecting to an HTML login page.");
            async Task<string> Token(string path = "/Account/Login")
            {
                var response = await client.GetAsync(path);
                var html = await response.Content.ReadAsStringAsync();
                var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
                check(response.IsSuccessStatusCode && token.Success, "The real Razor form must render a CSRF token.");
                return WebUtility.HtmlDecode(token.Groups[1].Value);
            }
            Task<HttpResponseMessage> Post(string path, params (string Key, string Value)[] fields) => client.PostAsync(path,
                new FormUrlEncodedContent(fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value))));
            async Task<HttpResponseMessage> Login(string secret, string destination = "/Home/Workspace") => await Post("/Account/Login",
                ("__RequestVerificationToken", await Token()), ("email", email), ("password", secret), ("returnUrl", destination));
            check((await Post("/Account/Login", ("email", email), ("password", password))).StatusCode == HttpStatusCode.BadRequest,
                "A real login POST without CSRF must be rejected.");
            var wrong = await Login("WrongPassword!123");
            check(wrong.IsSuccessStatusCode && (await wrong.Content.ReadAsStringAsync()).Contains("Invalid email or password"), "Incorrect credentials must fail in the real Identity pipeline.");
            check(await db.LoginHistories.AsNoTracking().AnyAsync(h => h.UserId == user.Id && !h.Succeeded), "Incorrect credentials must be recorded in SQL login history.");
            var correct = await Login(password);
            check(correct.StatusCode == HttpStatusCode.Redirect && correct.Headers.Location?.OriginalString == "/Home/Workspace", "Correct credentials must preserve a local return URL.");
            var cookie = string.Join(";", correct.Headers.GetValues("Set-Cookie"));
            check(cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) && cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase),
                "Authentication cookies must remain Secure, HttpOnly and SameSite=Lax.");
            var applicationCookie = correct.Headers.GetValues("Set-Cookie").First(value => value.StartsWith(".AspNetCore.Identity.Application="));
            check(!applicationCookie.Contains("max-age", StringComparison.OrdinalIgnoreCase) && !applicationCookie.Contains("expires=", StringComparison.OrdinalIgnoreCase),
                "A nonpersistent login must issue a browser-session cookie while the ticket retains its configured expiration.");
            check((await client.GetAsync("/api/staff")).StatusCode == HttpStatusCode.Forbidden,
                "An authenticated employee must receive 403 from staff administration APIs.");
            check((await client.GetAsync("/Home/Workspace")).IsSuccessStatusCode, "A real authentication cookie must permit protected navigation.");
            check((await client.GetAsync("/Staff")).Headers.Location?.OriginalString.Contains("AccessDenied") == true, "Employee login must not grant staff administration access.");
            check((await Post("/Account/Logout")).StatusCode == HttpStatusCode.BadRequest, "Logout must require CSRF protection.");
            await Post("/Account/Logout", ("__RequestVerificationToken", await Token("/Home/Workspace")));
            db.ChangeTracker.Clear(); user = (await users.FindByIdAsync(user.Id))!;
            user.IsActive = false; await users.UpdateAsync(user);
            check((await Login(password)).IsSuccessStatusCode, "Disabled-account login must remain on the form.");
            check((await client.GetAsync("/Home/Workspace")).StatusCode == HttpStatusCode.Redirect, "Disabled accounts must not receive an application session.");
            user.IsActive = true; await users.UpdateAsync(user);
            await users.SetLockoutEnabledAsync(user, true); await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));
            check((await Login(password)).IsSuccessStatusCode && (await client.GetAsync("/Home/Workspace")).StatusCode == HttpStatusCode.Redirect,
                "Locked accounts must not receive an application session.");
            await users.SetLockoutEndDateAsync(user, null);
            var external = await Login(password, "https://example.invalid/steal");
            check(external.StatusCode == HttpStatusCode.Redirect && external.Headers.Location?.IsAbsoluteUri == false,
                "An external return URL must never become a login redirect.");
            await Post("/Account/Logout", ("__RequestVerificationToken", await Token("/Home/Workspace")));
            db.ChangeTracker.Clear(); user = (await users.FindByIdAsync(user.Id))!;
            await users.ResetAuthenticatorKeyAsync(user); await users.SetTwoFactorEnabledAsync(user, true);
            var recovery = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 1))!.Single();
            var challenge = await Login(password);
            check(challenge.Headers.Location?.OriginalString.Contains("VerifyTwoFactor") == true, "MFA-enabled accounts must enter a challenge after password verification.");
            var invalidCode = await Post("/Account/VerifyTwoFactor", ("__RequestVerificationToken", await Token("/Account/VerifyTwoFactor")), ("code", "invalid-code"));
            check(invalidCode.IsSuccessStatusCode && (await client.GetAsync("/Home/Workspace")).StatusCode == HttpStatusCode.Redirect,
                "An invalid MFA code must not establish a session.");
            var recovered = await Post("/Account/RecoveryCode", ("__RequestVerificationToken", await Token("/Account/RecoveryCode")), ("code", recovery));
            check(recovered.StatusCode == HttpStatusCode.Redirect && (await client.GetAsync("/Home/Workspace")).IsSuccessStatusCode,
                "A valid recovery code must complete the real MFA challenge.");
            check(!string.Join(";", recovered.Headers.GetValues("Set-Cookie")).Contains("TwoFactorRememberMe"), "Completing MFA must not remember the browser silently.");
            db.ChangeTracker.Clear(); user = (await users.FindByIdAsync(user.Id))!;
            check(await users.CountRecoveryCodesAsync(user) == 0, "A recovery code must be consumed in SQL.");
            // Tokens must use the host's protected key ring and application name;
            // the parent runner otherwise deliberately uses an ephemeral provider.
            var sharedProtection = Microsoft.AspNetCore.DataProtection.DataProtectionProvider.Create(new DirectoryInfo(keyDirectory),
                builder => Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions.SetApplicationName(builder, "EduFlow"));
            users.RegisterTokenProvider(TokenOptions.DefaultProvider, new DataProtectorTokenProvider<ApplicationUser>(sharedProtection,
                Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance));
            var reset = await users.GeneratePasswordResetTokenAsync(user);
            var replacement = "Reset!" + Guid.NewGuid().ToString("N") + "1a";
            var resetPage = "/Account/ResetPassword?email=" + Uri.EscapeDataString(email) + "&token=" + Uri.EscapeDataString(reset);
            var resetResponse = await Post("/Account/ResetPassword", ("__RequestVerificationToken", await Token(resetPage)), ("email", email), ("token", reset), ("newPassword", replacement), ("confirmPassword", replacement));
            check(resetResponse.IsSuccessStatusCode && (await resetResponse.Content.ReadAsStringAsync()).Contains("Password updated"), "A valid password-reset token must complete the real reset workflow.");
            db.ChangeTracker.Clear(); user = (await users.FindByIdAsync(user.Id))!;
            check(await users.CheckPasswordAsync(user, replacement) && !await users.CheckPasswordAsync(user, password), "Password reset must persist the replacement hash and reject the old password.");
            check((await client.GetAsync("/Home/Workspace")).StatusCode == HttpStatusCode.Redirect, "Password reset must invalidate the existing application cookie.");
            HttpResponseMessage limited = null!;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                limited = await Login(replacement);
                if ((int)limited.StatusCode == 429) break;
            }
            check((int)limited.StatusCode == 429, "Repeated authentication requests must hit the real endpoint limiter.");
            check(await db.LoginHistories.AsNoTracking().AnyAsync(h => h.UserId == user.Id && h.Succeeded), "Successful authentication must be recorded in SQL login history.");
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            File.Delete(certificatePath); // Only the explicitly generated test certificate.
        }
    }
}
