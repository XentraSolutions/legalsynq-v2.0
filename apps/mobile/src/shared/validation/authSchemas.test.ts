import { loginSchema } from './authSchemas';

describe('loginSchema', () => {
  it('accepts a valid login payload', () => {
    expect(
      loginSchema.parse({
        email: 'demo@legalsynq.com',
        password: 'password123',
        tenantCode: 'demo',
      })
    ).toEqual({
      email: 'demo@legalsynq.com',
      password: 'password123',
      tenantCode: 'demo',
    });
  });

  it('rejects invalid email and short password values', () => {
    const result = loginSchema.safeParse({
      email: 'not-email',
      password: 'short',
    });

    expect(result.success).toBe(false);
  });
});
