"use client";

import { Toaster as Sonner, type ToasterProps } from "sonner";

const icons = {
  success: <i className="ri-checkbox-circle-fill text-green-500 text-base" />,
  error: <i className="ri-error-warning-fill text-red-500 text-base" />,
  warning: <i className="ri-alert-fill text-amber-500 text-base" />,
  info: <i className="ri-information-fill text-blue-500 text-base" />,
  loading: <i className="ri-loader-4-line animate-spin text-gray-400 text-base" />,
};

export function Toaster(props: ToasterProps) {
  return (
    <Sonner
      theme="light"
      position="bottom-right"
      closeButton
      icons={icons}
      toastOptions={{
        classNames: {
          toast:
            "group rounded-xl border border-gray-200 bg-white shadow-lg px-4 py-3",
          title: "text-sm font-medium text-gray-900",
          description: "text-xs text-gray-500 mt-0.5",
          actionButton: "sonner-action-button text-xs font-medium",
          cancelButton:
            "bg-gray-100 text-gray-600 hover:bg-gray-200 rounded-lg text-xs font-medium",
        },
      }}
      {...props}
    />
  );
}
