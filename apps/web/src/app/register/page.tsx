import { RegistrationForm } from './registration-form';
import Image from 'next/image';

export const metadata = { title: 'Register Your Tenant | LegalSynq' };

export default function RegisterPage() {
  return (
    <main className="relative min-h-screen overflow-hidden bg-[#f5f5f5] px-4 py-10 text-[#0a0a0a]">
      <div aria-hidden="true" className="pointer-events-none absolute inset-y-0 left-1/2 w-[1210px] max-w-full -translate-x-1/2 [background-image:linear-gradient(to_right,#f5f5f5_0%,rgba(245,245,245,0.5)_18%,rgba(245,245,245,0)_50%,rgba(245,245,245,0.5)_82%,#f5f5f5_100%),linear-gradient(rgba(229,229,229,0.42)_1px,transparent_1px),linear-gradient(90deg,rgba(229,229,229,0.42)_1px,transparent_1px)] [background-size:100%_100%,48px_48px,48px_48px]" />
      <div className="relative mx-auto flex max-w-[504px] flex-col items-center gap-8">
        <Image src="/legalsynq-logo.png" alt="LegalSynq" width={141} height={40} priority className="h-10 w-auto" />
        <RegistrationForm />
      </div>
    </main>
  );
}
