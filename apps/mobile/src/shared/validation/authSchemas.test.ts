import { loginSchema, returningLoginSchema, tenantCodeSchema } from './authSchemas';

describe('loginSchema', () => {
  it('accepts a valid login payload', () => {
    expect(
      loginSchema.parse({
        email: 'avery.mendoza@smithlaw.example',
        password: 'ValidPass123',
        tenantCode: 'smith-law',
      })
    ).toEqual({
      email: 'avery.mendoza@smithlaw.example',
      password: 'ValidPass123',
      tenantCode: 'smith-law',
    });
  });

  it('rejects invalid email and short password values', () => {
    const result = loginSchema.safeParse({
      email: 'not-email',
      password: 'short',
    });

    expect(result.success).toBe(false);
  });

  it('allows returning login payloads without tenant code', () => {
    expect(
      returningLoginSchema.parse({
        email: 'avery.mendoza@smithlaw.example',
        password: 'ValidPass123',
      })
    ).toEqual({
      email: 'avery.mendoza@smithlaw.example',
      password: 'ValidPass123',
    });
  });

  it('validates tenant-code-only payloads for local tenant add', () => {
    expect(tenantCodeSchema.parse({ tenantCode: ' smith-law ' })).toEqual({
      tenantCode: 'smith-law',
    });
  });
});
