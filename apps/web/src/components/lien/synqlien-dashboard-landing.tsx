import type { ReactNode } from 'react';

const stats = [
  {
    label: 'Total Lien Pending',
    value: '14',
    delta: '+8.9%',
    trend: 'up',
    helper: 'Up 8.9% vs Apr 1 - Apr 30',
  },
  {
    label: 'Total Pending Offered',
    value: '$185,000.00',
    delta: '+6.4%',
    trend: 'up',
    helper: 'Up 6.4% vs Apr 1 - Apr 30',
  },
  {
    label: 'Purchased Liens',
    value: '142',
    delta: '+14.2%',
    trend: 'up',
    helper: 'Up 14.2% vs Apr 1 - Apr 30',
  },
  {
    label: 'Capital Deployed',
    value: '$2,450,000.00',
    delta: '-5.0%',
    trend: 'down',
    helper: 'Down 5.0% vs Apr 1 - Apr 30',
  },
];

const pendingOffers = [
  {
    provider: 'Greenfield Medical Center',
    firm: 'Parker & Associates LLP',
    amount: '$142,500',
    time: '03/15/2025 - 9:30:00 AM',
  },
  {
    provider: 'Summit Rehabilitation Hospital',
    firm: 'Hartwell, Dunne & Cole',
    amount: '$87,200',
    time: '06/22/2025 - 2:15:00 PM',
  },
  {
    provider: 'Lakewood Surgical Center',
    firm: 'Brenton Chase Law Group',
    amount: '$214,750',
    time: '11/08/2024 - 11:45:00 AM',
  },
  {
    provider: 'Cedar Ridge Orthopedics',
    firm: 'Monroe & Whitfield PLLC',
    amount: '$63,900',
    time: '01/03/2025 - 4:00:00 PM',
  },
  {
    provider: 'Bayshore Spine & Joint Clinic',
    firm: 'Caldwell Briggs Attorneys',
    amount: '$175,000',
    time: '09/17/2024 - 8:20:00 AM',
  },
];

const pipeline = [
  {
    label: 'Pending',
    count: 75,
    percent: '37.5%',
    width: '37.5%',
    color: 'bg-[#eab308]',
    icon: 'ri-time-line',
    iconColor: 'text-[#a16207]',
  },
  {
    label: 'Accepted',
    count: 100,
    percent: '50%',
    width: '50%',
    color: 'bg-[#22c55e]',
    icon: 'ri-checkbox-circle-line',
    iconColor: 'text-[#15803d]',
  },
  {
    label: 'Declined',
    count: 25,
    percent: '12.5%',
    width: '12.5%',
    color: 'bg-[#ef4444]',
    icon: 'ri-close-circle-line',
    iconColor: 'text-[#dc2626]',
  },
];

export function SynqLienDashboardLanding() {
  return (
    <div className="mx-auto flex w-full max-w-[1440px] flex-col gap-6 text-neutral-950">
      <section className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
        <div>
          <h1 className="text-[32px] font-bold leading-tight tracking-normal">Dashboard</h1>
          <p className="mt-2 max-w-[720px] text-sm leading-6 text-neutral-500">
            Manage and monitor lien offers submitted to your company. Review opportunities,
            track activity, and take action from one centralized dashboard.
          </p>
        </div>
        <button
          type="button"
          className="inline-flex h-[38px] items-center justify-center rounded-[10px] bg-[#ee7132] px-5 text-sm font-medium text-white shadow-sm hover:bg-[#d96227]"
        >
          Offer Inbox
        </button>
      </section>

      <section className="grid gap-6 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat) => (
          <article
            key={stat.label}
            className="rounded-2xl border border-neutral-200 bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]"
          >
            <div className="flex items-start justify-between gap-3">
              <p className="text-sm leading-5 text-neutral-500">{stat.label}</p>
              <span
                className={
                  stat.trend === 'up'
                    ? 'inline-flex items-center gap-1 rounded-full bg-[#17c964]/15 px-2 py-0.5 text-xs font-medium text-[#15803d]'
                    : 'inline-flex items-center gap-1 rounded-full bg-[#ef4444]/10 px-2 py-0.5 text-xs font-medium text-[#dc2626]'
                }
              >
                <i
                  className={stat.trend === 'up' ? 'ri-trending-up-line' : 'ri-trending-down-line'}
                  aria-hidden
                />
                {stat.delta}
              </span>
            </div>
            <p className="mt-3 text-2xl font-bold leading-8">{stat.value}</p>
            <p className="mt-5 text-xs font-bold text-neutral-950">
              Trending {stat.trend} this month{' '}
              <i
                className={stat.trend === 'up' ? 'ri-trending-up-line' : 'ri-trending-down-line'}
                aria-hidden
              />
            </p>
            <p className="mt-1 text-xs leading-5 text-neutral-500">{stat.helper}</p>
          </article>
        ))}
      </section>

      <section className="grid gap-6 xl:grid-cols-2">
        <DashboardCard title="Pending Offers" actionLabel="View All">
          <div className="divide-y divide-neutral-200">
            {pendingOffers.map((offer) => (
              <div key={`${offer.provider}-${offer.amount}`} className="py-4 first:pt-0 last:pb-0">
                <div className="flex items-start justify-between gap-4">
                  <div className="min-w-0">
                    <StatusPill>Pending</StatusPill>
                    <p className="mt-3 truncate text-base font-semibold leading-5 text-neutral-950">
                      {offer.provider}
                    </p>
                    <p className="mt-2 truncate text-sm leading-5 text-neutral-500">{offer.firm}</p>
                  </div>
                  <div className="shrink-0 text-right">
                    <p className="text-base font-semibold leading-5 text-neutral-950">{offer.amount}</p>
                    <p className="mt-3 text-sm leading-5 text-neutral-500">{offer.time}</p>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </DashboardCard>

        <DashboardCard title="Today's Appointment" actionLabel="View All" className="min-h-[600px]">
          <EmptyState
            icon="ri-calendar-line"
            title="No Appointments Today"
            description="There are no appointments scheduled for today. Any upcoming appointments will appear here."
          />
        </DashboardCard>
      </section>

      <section className="flex flex-col gap-4">
        <div>
          <h2 className="text-2xl font-bold leading-8">Performance Overview</h2>
          <p className="mt-2 text-sm leading-5 text-neutral-500">Jun 15, 2026 - Jul 14, 2026</p>
        </div>

        <div className="grid h-10 grid-cols-3 rounded-xl bg-neutral-50 p-1 text-sm text-neutral-500 xl:max-w-[720px]">
          <button type="button" className="rounded-lg px-3">Last 7 Days</button>
          <button
            type="button"
            className="rounded-lg bg-white px-3 font-medium text-neutral-950 shadow-[0_1px_3px_rgba(0,0,0,0.16)]"
          >
            Last 30 Days
          </button>
          <button type="button" className="rounded-lg px-3">Custom</button>
        </div>

        <div className="grid gap-6 xl:grid-cols-2">
          <DashboardCard title="Acquisition Pipeline">
            <div className="space-y-6">
              <div className="flex items-end justify-between gap-4">
                <p className="text-2xl font-bold leading-8">Total:</p>
                <p className="text-2xl font-bold leading-8">200</p>
              </div>

              <div className="space-y-6">
                {pipeline.map((item) => (
                  <div key={item.label} className="space-y-3">
                    <div className="flex items-center justify-between gap-4">
                      <div className="flex items-center gap-3">
                        <span className="inline-flex h-8 w-8 items-center justify-center rounded-lg bg-neutral-100">
                          <i className={`${item.icon} ${item.iconColor}`} aria-hidden />
                        </span>
                        <span className="text-base font-medium">{item.label}</span>
                      </div>
                      <p className="text-base font-medium">
                        {item.count}{' '}
                        <span className="text-neutral-500">({item.percent})</span>
                      </p>
                    </div>
                    <div className="h-2 overflow-hidden rounded-full bg-neutral-100">
                      <div className={`h-full rounded-full ${item.color}`} style={{ width: item.width }} />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </DashboardCard>

          <DashboardCard title="Appointment Performance">
            <EmptyState
              icon="ri-calendar-line"
              title="No Appointment Performance"
              description="There is no appointment performance data found for this date range."
            />
          </DashboardCard>
        </div>
      </section>

      <section className="overflow-hidden rounded-2xl border border-neutral-200 bg-white shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
        <div className="flex items-center gap-3 border-b border-neutral-200 px-4 py-4">
          <i className="ri-more-2-fill text-neutral-400" aria-hidden />
          <h2 className="text-base font-semibold leading-5">Funding Company Performance</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-[720px] w-full text-left text-sm">
            <thead className="bg-neutral-100 text-neutral-950">
              <tr>
                <th className="px-4 py-3 font-medium">Funding Company</th>
                <th className="px-4 py-3 font-medium">Offered Liens</th>
                <th className="px-4 py-3 font-medium">Acceptance</th>
                <th className="px-4 py-3 font-medium">Appointments Completed</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-t border-neutral-200">
                <td className="px-4 py-4 font-medium">Meridian Capital Group</td>
                <td className="px-4 py-4 font-medium">50</td>
                <td className="px-4 py-4 font-medium">50%</td>
                <td className="px-4 py-4 font-medium">0</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section className="grid gap-6 xl:grid-cols-2">
        <ShortcutCard
          icon="ri-mail-line"
          title="Offer Inbox"
          description="Review and accept incoming lien offers."
        />
        <ShortcutCard
          icon="ri-calendar-line"
          title="Schedule"
          description="View today's and upcoming appointments"
        />
      </section>
    </div>
  );
}

function DashboardCard({
  title,
  actionLabel,
  className,
  children,
}: {
  title: string;
  actionLabel?: string;
  className?: string;
  children: ReactNode;
}) {
  return (
    <article
      className={`flex flex-col rounded-2xl border border-neutral-200 bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] ${className ?? ''}`}
    >
      <div className="mb-6 flex items-center justify-between gap-4">
        <h2 className="text-base font-semibold leading-5">{title}</h2>
        {actionLabel ? (
          <button
            type="button"
            className="inline-flex h-9 items-center overflow-hidden rounded-[10px] border border-neutral-200 text-sm font-medium text-neutral-950 shadow-sm hover:bg-neutral-50"
          >
            <span className="px-4">{actionLabel}</span>
            <span className="inline-flex h-full w-9 items-center justify-center border-l border-neutral-200">
              <i className="ri-arrow-right-line" aria-hidden />
            </span>
          </button>
        ) : null}
      </div>
      <div className="flex flex-1 flex-col">{children}</div>
    </article>
  );
}

function EmptyState({
  icon,
  title,
  description,
}: {
  icon: string;
  title: string;
  description: string;
}) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center px-4 py-16 text-center">
      <span className="inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-neutral-100">
        <i className={`${icon} text-3xl text-neutral-950`} aria-hidden />
      </span>
      <h3 className="mt-6 text-xl font-bold leading-7">{title}</h3>
      <p className="mt-2 max-w-[425px] text-base leading-6 text-neutral-500">{description}</p>
    </div>
  );
}

function StatusPill({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex h-6 items-center rounded-full bg-[#eab308]/15 px-2 text-xs font-medium text-[#a16207]">
      {children}
    </span>
  );
}

function ShortcutCard({
  icon,
  title,
  description,
}: {
  icon: string;
  title: string;
  description: string;
}) {
  return (
    <article className="rounded-2xl border border-neutral-200 bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)]">
      <div className="flex items-start gap-6">
        <span className="inline-flex h-12 w-12 shrink-0 items-center justify-center rounded-[10px] bg-neutral-100">
          <i className={`${icon} text-2xl text-neutral-950`} aria-hidden />
        </span>
        <div className="min-w-0">
          <h2 className="text-base font-semibold leading-5">{title}</h2>
          <p className="mt-2 text-sm leading-6 text-neutral-500">{description}</p>
        </div>
      </div>
    </article>
  );
}
