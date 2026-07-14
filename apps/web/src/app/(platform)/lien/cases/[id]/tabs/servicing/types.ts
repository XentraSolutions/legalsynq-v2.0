export type ServicingSubTab = "servicing-details" | "settlement-details" | "history";

export const SERVICING_SUB_TABS: {
  key: ServicingSubTab;
  label: string;
  icon: string;
}[] = [
  {
    key: "servicing-details",
    label: "Servicing Details",
    icon: "ri-settings-3-line",
  },
  {
    key: "settlement-details",
    label: "Settlement Details",
    icon: "ri-money-dollar-circle-line",
  },
  { key: "history", label: "History", icon: "ri-history-line" },
];
