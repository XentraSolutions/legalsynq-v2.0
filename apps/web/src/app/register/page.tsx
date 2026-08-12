import { RegistrationForm } from './registration-form';

export const metadata = { title: 'Register Your Tenant | LegalSynq' };

export default function RegisterPage() {
  return (
    <main className="min-h-screen bg-[#f5f5f5] px-4 py-10 text-[#0a0a0a] [background-image:linear-gradient(#e9e9e9_1px,transparent_1px),linear-gradient(90deg,#e9e9e9_1px,transparent_1px)] [background-size:48px_48px]">
      <div className="mx-auto flex max-w-[504px] flex-col items-center gap-8">
        <div className="rounded-xl bg-[#0f1928] px-5 py-3 text-xl font-semibold tracking-tight text-white">Legal<span className="text-[#ee7132]">Synq</span></div>
        <RegistrationForm />
      </div>
    </main>
  );
}
