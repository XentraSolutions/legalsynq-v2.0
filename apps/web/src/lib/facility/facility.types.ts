export interface FacilityGenericResponse {
  message: string
}

export interface CreateFacilityRequest {
  name: string
  email: string
  address: string
  city: string
  state: string
  zipcode: string
}

export interface CreateFacilityResponse extends FacilityGenericResponse {}

export interface FacilityListResponse {
  phone: string
}

export interface ContactPersonRequest {
  firstname: string
  lastname: string
  email: string
  facilityId: string
}

export interface ContactPersonResponse extends FacilityGenericResponse {}

export interface GetContactPersonByFacilityResponse {
  firstname: string
  lastname: string
}