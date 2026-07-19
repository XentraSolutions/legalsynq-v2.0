[x] - case and lien staus default preselect, first option, or maybe whitelist known initial values, case = predemand, lien = open
[x] - servicing - payment history table - defered need reference image,
[x] - case's liens delete
[x] - case's liens incomplete/missing columns values
[x] - servicing liens unnecessary link style 
[x] - servicing liens table - missing values
[x] - servicing details - lawfirm dropdown, lawyer and case manager, should use the new contact dropdown, with lazy loading, same with case creation
[] - revisit liens filter and sorting - defered , still waiting for API
[] - revisit liens table on case after filter and sorting update , maybe improve , unify the API used, we wont need preloading if we have a better API
[] - case management , filter sorting , liens already mimic the legacy exactly assuming the API is in place, now case should also need to sync.
[x] - dates have inconsistent formatting when displayed, lets use the same format, create a datedisplay component so we can change it in one place, and use it everywhere, also make sure to use the same timezone, UTC or local, and be consistent. needs investigation if the tenant timezone is something we can apply since its available. DONE for lien/case/servicing domain: DateDisplay component (src/components/ui/date-display.tsx) + dedicated formatLegacy* functions in src/lib/format-date.ts (kept separate from the pre-existing shared formatDate* functions so other teams' product areas aren't affected), tenant timezone via useTimezone(), legacy MM/DD/YYYY default, no day-shifting on pure dates, naive-datetime-assumed-UTC. App-wide rollout to careconnect/notifications/control-center/insights/fund is deferred as a follow-up (out of scope for this session). A few lien-domain files with custom relative-time logic (note-utils.ts, notes-list-section.tsx, email-inbox/page.tsx) were intentionally left as-is since they don't fit a single fixed format.