import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const context = { window: {} };
vm.runInNewContext(readFileSync(new URL('../wwwroot/js/staff-resource-schemas.js', import.meta.url), 'utf8'), context);
const schemas = context.window.StaffResourceSchemas;
test('all five native workspace registers load without a frontend framework', () => {
    assert.deepEqual(Object.keys(schemas).sort(), ['accountant', 'hostel', 'librarian', 'registrar', 'transport']);
});
for (const [role, pages] of Object.entries(schemas)) {
    test(`${role} register forms have unique named fields and valid controls`, () => {
        for (const schema of Object.values(pages)) {
            assert.ok(schema.title && schema.noun);
            const names = schema.fields.map(field => field.name);
            assert.equal(new Set(names).size, names.length);
            for (const field of schema.fields) {
                assert.ok(field.name && field.label);
                assert.ok(!field.type || ['text', 'number', 'date', 'time', 'boolean', 'textarea'].includes(field.type));
                if (field.options) assert.ok(field.options.length > 0);
            }
        }
    });
}
test('shared resident attendance fields survive native script conversion', () => {
    assert.equal(schemas.hostel.attendance.fields, schemas.transport.attendance.fields);
    assert.ok(schemas.hostel.attendance.fields.some(field => field.name === 'studentId' && field.required));
});
