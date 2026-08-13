'use server';
import { revalidatePath } from 'next/cache';
import { controlCenterServerApi } from '@/lib/control-center-api';
export async function approveRegistration(id:string){const r=await controlCenterServerApi.tenantRegistrations.approve(id);revalidatePath(`/tenant-applications/${id}`);revalidatePath('/tenant-applications');return r;}
export async function declineRegistration(id:string,reason:string){if(!reason.trim())throw new Error('A decline reason is required.');const r=await controlCenterServerApi.tenantRegistrations.decline(id,reason);revalidatePath(`/tenant-applications/${id}`);revalidatePath('/tenant-applications');return r;}
export async function retryRegistration(id:string){const r=await controlCenterServerApi.tenantRegistrations.retry(id);revalidatePath(`/tenant-applications/${id}`);return r;}
