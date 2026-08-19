import { redirect } from "next/navigation";
import Link from "next/link";
import Image from "next/image";
import { fetchPublicCareConnect } from "../lib/public-referral-proxy";
import {
  mapFailureReasonToInvalidReason,
  readPublicReferralFailureReason,
} from "../lib/public-referral-error";

export const dynamic = "force-dynamic";

export default async function IntroductionPage({ searchParams }: any) {
  const sp = await searchParams;
  const token = sp.token?.trim() ?? "";

  if (!token) {
    redirect("/referrals/accept/invalid?reason=missing-token");
  }

  let threadData: any = null;
  let failureReason: string | null = null;

  try {
    const resp = await fetchPublicCareConnect(
      `/api/public/referrals/thread?token=${encodeURIComponent(token)}`,
    );
    if (resp.ok) {
      threadData = await resp.json();
    } else {
      failureReason = await readPublicReferralFailureReason(resp);
    }
  } catch {
    threadData = null;
  }

  if (!threadData) {
    redirect(
      `/referrals/accept/invalid?reason=${mapFailureReasonToInvalidReason(failureReason)}`,
    );
  }

  const activateUrl = `/referrals/activate?referralId=${threadData.referralId}&token=${encodeURIComponent(token)}&companyName=${encodeURIComponent(threadData.providerName)}`;

  const cardDetails = [
    {
      title: "Receive Referrals",
      icon: "ri-heart-pulse-line",
      description:
        "Receive and manage referrals from law firms and case managers",
    },
    {
      title: "Track Patient Care",
      icon: "ri-dossier-line",
      description:
        "Track appointments, treatment progress, and outcomes throughout the referral process.",
    },
    {
      title: "Communicate Securely",
      icon: "ri-shield-user-line",
      description:
        "Collaborate with your care network through secure, compliant communication.",
    },
    {
      title: "Stay Connected",
      icon: "ri-group-line",
      description:
        "Access one centralized portal for providers, specialists, and law firm partners.",
    },
  ];

  return (
    <main className="min-h-screen bg-[#F5F5F5]">
      <div className="max-w-2xl mx-auto px-4 py-12">
        <div className="mb-6">
          <Link
            href={`/referrals/thread?token=${encodeURIComponent(token)}`}
            className="text-sm text-gray-500 hover:text-gray-700 transition-colors bg-white rounded-full py-3 px-4"
          >
            ←
          </Link>
        </div>

        <div className="text-center mb-8">
          <div className="flex items-center justify-center gap-4 mb-6">
            <Image
              src="/legalsynq-logo.png"
              alt="LegalSynq"
              width={180}
              height={30}
              priority
              unoptimized
            />
            <div className="border-l border-gray-200 h-10" />

            <Image
              src="/careconnect-dark-logo.png"
              alt="LegalSynq"
              width={180}
              height={30}
              priority
              unoptimized
            />
          </div>

          <h1 className="text-5xl font-semibold text-gray-900 tracking-wide">
            Connect and manage referrals in one place
          </h1>
          <p className="mt-2 text-gray-500 max-w-md mx-auto">
            A centralized workspace for medical providers, specialists, and law
            firm partners coordinating patient care.
          </p>
        </div>

        <div className="grid grid-cols-2 gap-4 mb-10 px-8">
          {cardDetails.map((card) => (
            <div className="bg-white min-h-[120px] flex flex-col p-6 px-4 text-[#404040] rounded-2xl">
              <div>
                <i
                  className={`${card.icon} flex justify-end text-2xl text-[#EE7132]`}
                ></i>

                <h2 className="text-2xl font-semibold max-w-35 mb-3">
                  {card.title}
                </h2>
                <p className="text-md mb-3">{card.description}</p>
              </div>
            </div>
          ))}
        </div>

        <div className="text-center text-sm text-gray-600 my-2">
          Takes less than 10 minutes • Token secure link verified
        </div>

        <Link
          className="rounded-xl bg-primary text-white w-full p-2 my-2 block text-center"
          href={activateUrl}
        >
          Get Started <i className="ri-arrow-right-long-line"></i>
        </Link>

        <div className="text-center text-sm text-gray-600 my-2">
          Already have an activated portal account?
          <Link
            className="text-primary font-semibold ml-1 hover:underline"
            href={threadData?.loginUrl}
          >
            Log in
          </Link>
        </div>
      </div>
    </main>
  );
}
