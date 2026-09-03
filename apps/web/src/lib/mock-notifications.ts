// Personal notification center — mock data.
//
// The backend endpoint for this feature isn't ready yet (tracked separately
// from the tenant-admin email delivery dashboard at /notifications, which is
// a different, already-shipped feature). Until then this renders a safe,
// static shape instead of hitting an API that returns errors.

export type MockNotificationCategory = "lien" | "message";

export interface MockNotificationAvatar {
  initials: string;
  bg: string;
  color: string;
}

export interface MockNotification {
  id: string;
  category: MockNotificationCategory;
  title: string;
  description: string;
  timestamp: string; // ISO
  read: boolean;
  avatar: MockNotificationAvatar;
}

const AVATARS: Record<string, MockNotificationAvatar> = {
  summitFunding: { initials: "SF", bg: "#FDF1EB", color: "#C2620A" },
  apexMutual: { initials: "AM", bg: "#EFF6FF", color: "#2563EB" },
  velantrix: { initials: "VX", bg: "#F0FDF4", color: "#16A34A" },
  medfirst: { initials: "MC", bg: "#F5F3FF", color: "#7C3AED" },
  valleyMedical: { initials: "VC", bg: "#FEF2F2", color: "#DC2626" },
  sarahMitchell: { initials: "SM", bg: "#ECFEFF", color: "#0891B2" },
  jamesRivera: { initials: "JR", bg: "#FFFBEB", color: "#D97706" },
};

const MOCK_NOTIFICATIONS_SEED: MockNotification[] = [
  {
    id: "n1",
    category: "lien",
    title: "Lien Offer Accepted",
    description:
      "Summit Funding Group accepted your $80,780.00 offer submitted for Case #LN-8842.",
    timestamp: "2026-07-28T15:45:12",
    read: false,
    avatar: AVATARS.summitFunding,
  },
  {
    id: "n2",
    category: "lien",
    title: "Lien Offer Submitted",
    description:
      "Your lien offer of $34,125.00 was successfully sent to Velantrix and is currently under review.",
    timestamp: "2026-07-14T10:22:17",
    read: true,
    avatar: AVATARS.velantrix,
  },
  {
    id: "n3",
    category: "message",
    title: "New Message from Summit Funding",
    description:
      'Apex Mutual sent a message regarding Case #LN-4920: "Can you provide the updated medical records?"',
    timestamp: "2026-07-28T15:45:12",
    read: true,
    avatar: AVATARS.apexMutual,
  },
  {
    id: "n4",
    category: "lien",
    title: "Lien Offer Accepted",
    description:
      "Summit Funding has accepted your settlement offer of $42,500.00 for Case #LN-7831.",
    timestamp: "2026-08-29T10:15:33",
    read: true,
    avatar: AVATARS.summitFunding,
  },
  {
    id: "n5",
    category: "lien",
    title: "Lien Offer Declined",
    description:
      "Apex Mutual has declined the proposed offer of $18,750.00 for Case #LN-5204.",
    timestamp: "2026-08-28T14:37:48",
    read: true,
    avatar: AVATARS.apexMutual,
  },
  {
    id: "n6",
    category: "message",
    title: "New Message from Sarah Mitchell",
    description:
      'Sarah Mitchell sent a message regarding Case #LN-4920: "Hi, just following up on the lien verification documents."',
    timestamp: "2026-08-27T11:22:05",
    read: true,
    avatar: AVATARS.sarahMitchell,
  },
  {
    id: "n7",
    category: "lien",
    title: "Lien Offer Submitted",
    description:
      "Your lien offer of $34,125.00 was successfully sent to Velantrix and is currently under review.",
    timestamp: "2026-08-26T09:08:17",
    read: true,
    avatar: AVATARS.velantrix,
  },
  {
    id: "n8",
    category: "lien",
    title: "Lien Offer Accepted",
    description:
      "MedFirst Collections has accepted your settlement offer of $12,800.00 for Case #LN-3392.",
    timestamp: "2026-08-25T16:55:41",
    read: true,
    avatar: AVATARS.medfirst,
  },
  {
    id: "n9",
    category: "message",
    title: "Direct Message from James Rivera",
    description:
      'James Rivera sent a message regarding Case #LN-8015: "The updated billing records have been uploaded. Let me know if you need anything else."',
    timestamp: "2026-08-24T13:12:29",
    read: true,
    avatar: AVATARS.jamesRivera,
  },
  {
    id: "n10",
    category: "lien",
    title: "Lien Offer Declined",
    description:
      "Valley Medical Center has declined the proposed offer of $27,350.00 for Case #LN-9460.",
    timestamp: "2026-08-23T08:40:56",
    read: true,
    avatar: AVATARS.valleyMedical,
  },
  {
    id: "n11",
    category: "lien",
    title: "Lien Offer Submitted",
    description:
      "Your lien offer of $56,200.00 was successfully sent to MedFirst Collections and is currently under review.",
    timestamp: "2026-08-20T09:41:02",
    read: true,
    avatar: AVATARS.medfirst,
  },
  {
    id: "n12",
    category: "message",
    title: "New Message from Summit Funding",
    description:
      'Summit Funding sent a message regarding Case #LN-7831: "Settlement documents are ready for your review."',
    timestamp: "2026-08-19T17:03:44",
    read: true,
    avatar: AVATARS.summitFunding,
  },
];

// Only seeded in development — production/staging show the real empty
// state until the backend endpoint above ships.
export const MOCK_NOTIFICATIONS: MockNotification[] =
  process.env.NODE_ENV === "development" ? MOCK_NOTIFICATIONS_SEED : [];
