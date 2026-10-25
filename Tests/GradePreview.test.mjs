import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../Views/Mark/Create.cshtml', import.meta.url), 'utf8');
const start = source.indexOf('    function grade(score)');
const end = source.indexOf('    function refresh()', start);
const handler = source.slice(start, end).replace("Number('@Model.MaximumMarks.ToString(System.Globalization.CultureInfo.InvariantCulture)')", 'maximumMarks');
function grade(score, gradeScale = [], maximumMarks = 100) {
    return vm.runInNewContext(handler + '\ngrade(score);', { score, gradeScale, maximumMarks });
}
test('preview honors ordered department and institutional grade boundaries', () => {
    const scale = [{ grade: 'Distinction', minimumPercent: 70, maximumPercent: 100 }, { grade: 'Pass', minimumPercent: 50, maximumPercent: 100 }];
    assert.equal(grade('72', scale), 'Distinction');
    assert.equal(grade('55', scale), 'Pass');
});
test('fallback preview uses normalized marks and rejects out-of-range input', () => {
    assert.equal(grade('40', [], 50), 'A');
    assert.equal(grade('51', [], 50), '—');
    assert.equal(grade('', [], 50), '—');
});
