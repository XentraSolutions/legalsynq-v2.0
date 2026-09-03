import { DeepLinkResolver } from './DeepLinkResolver';

const resolver = new DeepLinkResolver({ expectedHttpsHost: 'links.qa.example.test' });

describe('DeepLinkResolver', () => {
  it('recognizes exact root as a benign generic portal entry', () => {
    expect(resolver.resolve('https://LINKS.QA.EXAMPLE.TEST/')).toEqual({
      status: 'portal_entry',
      originalUrl: 'https://LINKS.QA.EXAMPLE.TEST/',
      normalizedUrl: 'https://links.qa.example.test/',
    });
  });

  it('rejects query parameters and fragments on generic portal entry', () => {
    expect(resolver.resolve('https://links.qa.example.test/?source=test')).toMatchObject({
      status: 'invalid_parameters',
    });
    expect(resolver.resolve('https://links.qa.example.test/#dashboard')).toMatchObject({
      status: 'invalid_parameters',
    });
  });

  it('resolves the static dashboard route', () => {
    expect(resolver.resolve('https://links.qa.example.test/dashboard')).toEqual({
      status: 'resolved',
      routeKey: 'dashboard',
      pathParameters: {},
      queryParameters: {},
      originalUrl: 'https://links.qa.example.test/dashboard',
      normalizedUrl: 'https://links.qa.example.test/dashboard',
    });
  });

  const parameterizedRoutes: ReadonlyArray<readonly [string, string, string, string]> = [
    ['/deals/123', 'dealDetails', 'dealId', '123'],
    ['/contacts/contact-1', 'contactDetails', 'contactId', 'contact-1'],
    ['/applications/ABC-123', 'applicationDetails', 'applicationId', 'ABC-123'],
    ['/reports/r1', 'reportDetails', 'reportId', 'r1'],
  ];
  for (const [path, routeKey, parameterName, parameterValue] of parameterizedRoutes) {
    it(`resolves ${path} through the shared route registry`, () => {
      expect(resolver.resolve(`https://links.qa.example.test${path}`)).toMatchObject({
        status: 'resolved',
        routeKey,
        pathParameters: { [parameterName]: parameterValue },
      });
    });
  }

  it('decodes and canonically normalizes an encoded path parameter', () => {
    expect(resolver.resolve('https://LINKS.QA.EXAMPLE.TEST/deals/A%20B')).toMatchObject({
      status: 'resolved',
      pathParameters: { dealId: 'A B' },
      normalizedUrl: 'https://links.qa.example.test/deals/A%20B',
    });
  });

  for (const path of [
    '/deals',
    '/deals/',
    '/deals/%20',
    '/deals/123/extra',
    '/unknown/123',
    '/dashboard/',
    '//dashboard',
  ]) {
    it(`rejects an unsupported or structurally invalid path: ${path}`, () => {
      expect(resolver.resolve(`https://links.qa.example.test${path}`)).toMatchObject({
        status: 'unsupported_route',
      });
    });
  }

  for (const url of [
    'not a url',
    'https://links.qa.example.test/deals/%ZZ',
    'https://links.qa.example.test/deals/%C3%28',
  ]) {
    it(`safely rejects malformed URL input: ${url}`, () => {
      expect(resolver.resolve(url)).toMatchObject({ status: 'malformed' });
    });
  }

  it('rejects unsupported schemes without changing custom URL creation behavior', () => {
    expect(resolver.resolve('com.legalsynq.qa://deals/123')).toMatchObject({
      status: 'unsupported_scheme',
    });
    expect(resolver.resolve('http://links.qa.example.test/deals/123')).toMatchObject({
      status: 'unsupported_scheme',
    });
  });

  for (const url of [
    'https://other.example.test/deals/123',
    'https://links.qa.example.test:444/deals/123',
    'https://user@links.qa.example.test/deals/123',
  ]) {
    it(`rejects an unsupported HTTPS authority: ${url}`, () => {
      expect(resolver.resolve(url)).toMatchObject({ status: 'unsupported_host' });
    });
  }

  it('rejects HTTPS routing when the active environment has no configured host', () => {
    const unconfiguredResolver = new DeepLinkResolver({ expectedHttpsHost: null });

    expect(unconfiguredResolver.resolve('https://links.qa.example.test/dashboard')).toMatchObject({
      status: 'unsupported_host',
    });
  });

  for (const url of [
    'https://links.qa.example.test/dashboard?campaign=test',
    'https://links.qa.example.test/dashboard?x=1&x=2',
    'https://links.qa.example.test/dashboard?bad=%ZZ',
  ]) {
    it(`rejects undeclared or malformed query parameters: ${url}`, () => {
      expect(resolver.resolve(url)).toMatchObject({ status: 'invalid_parameters' });
    });
  }

  it('rejects fragments instead of allowing them to influence route identity', () => {
    expect(resolver.resolve('https://links.qa.example.test/dashboard#deals/123')).toMatchObject({
      status: 'invalid_parameters',
    });
  });

  it('rejects invalid resolver host configuration as a programming error', () => {
    expect(() => new DeepLinkResolver({ expectedHttpsHost: 'https://bad.test' })).toThrow(
      'must be a DNS hostname'
    );
  });
});
