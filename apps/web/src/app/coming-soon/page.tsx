"use client";
import { useRouter } from "next/navigation";

export default function ComingSoonPage() {
  const router = useRouter();
  const handleCloseOrBack = () => {
    // Ensure window object is available to avoid SSR issues
    if (typeof window !== "undefined") {
      if (window.history.length <= 1) {
        // Tab has no history, close it safely
        window.close();
      } else {
        // Tab has history, go back normally
        router.back();
      }
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center text-center p-8 bg-gray-50">
      <h1 className="font-bold mb-6">COMING SOON</h1>
      <p className="text-gray-500 mb-10">
        Official legal documents are not yet available.
      </p>
      <button
        type="button"
        className="flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-semibold text-white bg-primary transition-opacity disabled:opacity-60"
        onClick={() => handleCloseOrBack()}
      >
        Go Back
      </button>
    </div>
  );
}
