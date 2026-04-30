import { fetchEnrollmentPrefill } from './actions';
import { EnrollmentForm }         from './enrollment-form';

interface SearchParams {
  id?:       string;
  tenantId?: string;
}

interface PageProps {
  searchParams: Promise<SearchParams>;
}

export default async function EnrollPage({ searchParams }: PageProps) {
  const { id: providerId, tenantId } = await searchParams;

  let prefill = null;
  let alreadyEnrolled = false;

  if (providerId && tenantId) {
    try {
      prefill = await fetchEnrollmentPrefill(providerId, tenantId);
    } catch {
      // prefill stays null — form shows empty
    }
  }

  // If prefill is null but we have an id, the provider wasn't found or is already enrolled.
  // The EnrollmentForm handles both the pre-filled and fresh-start states.

  return (
    <main className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50">
      <div className="max-w-2xl mx-auto px-4 py-12">

        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-full bg-blue-100 mb-4">
            <i className="ri-shield-check-line text-2xl text-blue-600" />
          </div>
          <h1 className="text-3xl font-bold text-gray-900">Get Full Portal Access</h1>
          <p className="mt-2 text-gray-500 max-w-md mx-auto">
            Set up your CareConnect account to manage referrals, appointments, and
            communications — all in one place.
          </p>
        </div>

        <EnrollmentForm
          prefill={prefill}
          providerId={providerId ?? null}
          tenantId={tenantId ?? null}
        />

        <p className="text-center text-xs text-gray-400 mt-6">
          Already have an account?{' '}
          <a href="/login" className="text-blue-600 hover:underline">Sign in</a>
        </p>
      </div>
    </main>
  );
}
