import { ApiResponse } from "@/types";
import { liensGlobalSearch } from "./global-search.api";

export const lienGlobalService = {
  async globalSearch(query:any): Promise<any> {
    const { data } = await liensGlobalSearch.list({query: query});
    return {items: data.liens.items}
  }
}