import { facilityApi } from './facility.api';
import type { ContactPersonRequest, ContactPersonResponse, CreateFacilityRequest, CreateFacilityResponse, FacilityListResponse, GetContactPersonByFacilityResponse, UpdateContactPersonRequest } from './facility.types';

export const facilityService = {
  async createFacility(form: CreateFacilityRequest): Promise<CreateFacilityResponse> {
    const { data } = await facilityApi.createFacility(form)
    return data
  },
  async getFacilityList(): Promise<FacilityListResponse> {
    const { data } = await facilityApi.getFacilityList()
    return data
  },
  async contactPerson(form: ContactPersonRequest): Promise<ContactPersonResponse> {
    const { data } = await facilityApi.contactPerson(form)
    return data
  },
  async getContactPersonByFacility(facilityId: string): Promise<GetContactPersonByFacilityResponse> {
    const { data } = await facilityApi.getContactPersonByFacility(facilityId)
    return data
  },
  async updateContactPerson(form: UpdateContactPersonRequest): Promise<ContactPersonResponse> {
    const { data } = await facilityApi.updateContactPerson(form)
    return data
  },
  async deleteContactPerson(id: string): Promise<void> {
    await facilityApi.deleteContactPerson(id)
  }
}