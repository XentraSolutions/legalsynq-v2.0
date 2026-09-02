import { useState, useEffect, useCallback } from 'react';
import { useQuery } from '@tanstack/react-query';
import { liensGlobalSearch } from '@/lib/global-search/global-search.api';
import { lienGlobalService } from '@/lib/global-search/global-search.service';

export interface SearchResult {
  id: string;
  title: string;
  category: string;
  url: string;
}

export function useTanStackGlobalSearch(debounceMs = 300, minChars = 2) {
  const [inputValue, setInputValue] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [isOpen, setIsOpen] = useState(false);

  // 1. Manual debounce logic for the input value to protect global performance
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedQuery(inputValue.trim());
    }, debounceMs);

    return () => clearTimeout(handler);
  }, [inputValue, debounceMs]);

  // 2. TanStack Query handles caching, auto-abort signals, and background states
  const { data: results = [], isLoading, error } = useQuery({
    queryKey: ['globalSearch', debouncedQuery],
    queryFn: async ({ signal }) => {
      if (!debouncedQuery || debouncedQuery.length < minChars) return [];
      
      const response = await lienGlobalService.globalSearch(debouncedQuery);
      if (!response) throw new Error('Network response failed');
      return response.items
    },
    enabled: debouncedQuery.length >= minChars,
    staleTime: 1000 * 60 * 5, // Cache search results for 5 minutes
  });

  const clearSearch = useCallback(() => {
    setInputValue('');
    setDebouncedQuery('');
    setIsOpen(false);
  }, []);

  return {
    inputValue,
    setInputValue,
    results,
    isLoading: isLoading && debouncedQuery.length >= minChars,
    isOpen,
    setIsOpen,
    error: error ? 'Failed to fetch results' : null,
    clearSearch,
  };
}