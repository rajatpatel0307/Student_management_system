(function () {
    "use strict";

    const palette = {
        primary: ["#4f46e5", "rgba(79,70,229,.16)"],
        success: ["#059669", "rgba(5,150,105,.16)"],
        warning: ["#d97706", "rgba(217,119,6,.16)"],
        danger: ["#dc2626", "rgba(220,38,38,.14)"],
        info: ["#0284c7", "rgba(2,132,199,.15)"]
    };

    function formatValue(value, format) {
        if (format === "currency") return new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 0 }).format(value);
        if (format === "percent") return `${Number(value).toFixed(1)}%`;
        return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);
    }

    function render(canvas) {
        const node = document.getElementById(`chart-data-${canvas.dataset.dashboardChart}`);
        if (!node || typeof window.Chart === "undefined") return;
        let model;
        try { model = JSON.parse(node.textContent); } catch (_) { return; }
        const styles = getComputedStyle(document.documentElement);
        const text = styles.getPropertyValue("--text-muted").trim() || "#64748b";
        const grid = styles.getPropertyValue("--border-color").trim() || "#e2e8f0";
        const datasets = model.datasets.map(function (series) {
            const colors = palette[series.tone] || palette.primary;
            return {
                label: series.label,
                data: series.values,
                type: series.type || undefined,
                yAxisID: series.axis || undefined,
                borderColor: colors[0],
                backgroundColor: model.type === "doughnut" ? undefined : colors[1],
                borderWidth: 2,
                pointRadius: 2,
                tension: .28,
                fill: Boolean(series.fill)
            };
        });
        if (model.type === "doughnut" && datasets.length) {
            datasets[0].backgroundColor = Object.values(palette).map(x => x[0]);
            datasets[0].borderWidth = 0;
        }
        new window.Chart(canvas, {
            type: model.type,
            data: { labels: model.labels, datasets: datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: model.horizontal ? "y" : "x",
                animation: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? false : { duration: 450 },
                interaction: { mode: "index", intersect: false },
                plugins: {
                    legend: { position: "bottom", labels: { color: text, usePointStyle: true, boxWidth: 8 } },
                    tooltip: { callbacks: { label: context => {
                        const value = model.horizontal ? context.parsed.x : (context.parsed.y ?? context.parsed);
                        return `${context.dataset.label}: ${formatValue(value, model.format)}`;
                    } } }
                },
                scales: model.type === "doughnut" ? {} : {
                    x: { stacked: model.stacked, ticks: { color: text, maxRotation: 0, autoSkip: true }, grid: { display: false } },
                    y: { stacked: model.stacked, beginAtZero: true, ticks: { color: text, callback: value => formatValue(value, model.format) }, grid: { color: grid } }
                }
            }
        });
    }

    document.querySelectorAll("[data-dashboard-chart]").forEach(render);
    document.querySelectorAll("[data-dashboard-print]").forEach(button => button.addEventListener("click", () => window.print()));
}());
