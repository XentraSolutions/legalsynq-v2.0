import { ApiResponse } from "@/types";
import { liensGlobalSearch } from "./global-search.api";

export function mapToListItem(dto: any): any {
  return [
    {
      title: "Plaintiff Name",
      items: dto.plaintiffs.map((c: any) => ({
        id: c.id,
        url: `/lien/cases/${c.caseId}/details`,
        name: c.plaintiffName,
        details: [
          { title: "Case Id", description: c.caseCode },
          { title: "Date Of Loss", description: c.dateOfLoss },
          { title: "Date Of Birth", description: c.dateOfBirth },
        ].filter((d) => d.description), // removes empty/null fields automatically
      })),
    },
    {
      title: "Medical Providers",
      items: dto.medicalProviders.map((c: any) => ({
        id: c.id,
        url: `/lien/contacts/${c.contactId}/overview`,
        name: c.name,
        details: [
          { title: "Active Cases", description: c.activeCases },
          { title: "Address", description: c.address },
        ].filter((d) => d.description), // removes empty/null fields automatically
      })),
    },
    {
      title: "Medical Facilities",
      items: dto.medicalFacilities.map((c: any) => ({
        id: c.id,
        url: `/lien/contacts/${c.contactId}/overview`,
        name: c.name,
        details: [
          { title: "Active Cases", description: c.activeCases },
          { title: "Address", description: c.address },
        ].filter((d) => d.description), // removes empty/null fields automatically
      })),
    },
    {
      title: "Funding Companies",
      items: dto.fundingCompanies.map((c: any) => ({
        id: c.id,
        url: `/lien/contacts/${c.contactId}/overview`,
        name: c.name,
        details: [
          { title: "Active Cases", description: c.activeCases },
          { title: "Address", description: c.address },
        ].filter((d) => d.description), // removes empty/null fields automatically
      })),
    },
    {
      title: "Leads",
      items: dto.Leads.map((c: any) => ({
        id: c.id,
        url: `/lien/contacts/${c.contactId}/overview`,
        name: c.name,
        details: [
          { title: "Active Cases", description: c.activeCases },
          { title: "Address", description: c.address },
        ].filter((d) => d.description), // removes empty/null fields automatically
      })),
    },
    {
      title: "Servicing",
      items: dto.servicing.map((c: any) => ({
        id: c.id,
        url: `/lien/cases/${c.caseId}/servicing`,
        name: c.plaintiffName,
        details: [
          { title: "Case Id", description: c.caseCode },
          { title: "Current Status", description: c.currentStatus },
          { title: "Settlement Status", description: c.settlementStatus },
        ].filter((d) => d.description), // removes empty/null fields automatically
      })),
    },
    {
      title: "Law Firms",
      items: dto.lawFirms.map((c: any) => ({
        id: c.id,
        url: `/lien/contacts/${c.contactId}/overview`,
        name: c.name,
        details: [
          { title: "Active Cases", description: c.activeCases },
          { title: "Address", description: c.address },
        ].filter((d) => d.description), // removes empty/null fields automatically
      })),
    },
  ];
}

export const lienGlobalService = {
  async globalSearch(query: any): Promise<any> {
    const { data } = await liensGlobalSearch.list({ query: query });
    return { items: mapToListItem(data) };
  },
};
