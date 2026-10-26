"use client";

import { CalendarDays, Search, X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import type { DateRange } from "react-day-picker";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { calendarToApiDate, formatApiDate, lastDays, parseApiDate } from "@/lib/dates";
import { useHistoryFilters, type TransactionType } from "./useHistoryFilters";

export const SEARCH_DEBOUNCE_MS = 300;
export const SEARCH_MIN_LENGTH = 2;

function rangeLabel(from: string | null, to: string | null) {
  if (from && to) return `${formatApiDate(from)} – ${formatApiDate(to)}`;
  if (from) return `From ${formatApiDate(from)}`;
  if (to) return `Until ${formatApiDate(to)}`;
  return "All dates";
}

export function HistoryFilters() {
  const { filters, setFilters, active, clear } = useHistoryFilters();
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState(filters.q ?? "");
  const lastSent = useRef(filters.q ?? "");

  // URL → box, when the URL changes from elsewhere (Clear filters, chip removal, Back).
  useEffect(() => {
    if ((filters.q ?? "") !== lastSent.current) {
      lastSent.current = filters.q ?? "";
      setSearch(filters.q ?? "");
    }
  }, [filters.q]);

  // Box → URL, debounced; the API only gets a search of 2 or more characters.
  useEffect(() => {
    const handle = setTimeout(() => {
      const trimmed = search.trim();
      const q = trimmed.length >= SEARCH_MIN_LENGTH ? trimmed : null;
      if ((q ?? "") !== (filters.q ?? "")) {
        lastSent.current = q ?? "";
        void setFilters({ q });
      }
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
  }, [search, filters.q, setFilters]);

  const selected: DateRange | undefined =
    filters.from || filters.to
      ? {
          from: filters.from ? parseApiDate(filters.from) : undefined,
          to: filters.to ? parseApiDate(filters.to) : undefined,
        }
      : undefined;

  const applyPreset = (days: number | null) => {
    void setFilters(days === null ? { from: null, to: null } : lastDays(days));
    setOpen(false);
  };

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-3 md:flex-row md:items-end">
        <div className="flex flex-col gap-2">
          <span className="text-sm font-medium" id="date-range-label">
            Date range
          </span>
          <Popover open={open} onOpenChange={setOpen}>
            <PopoverTrigger asChild>
              <Button
                variant="outline"
                className="justify-start font-normal"
                aria-labelledby="date-range-label date-range-value"
              >
                <CalendarDays aria-hidden />
                <span id="date-range-value">{rangeLabel(filters.from, filters.to)}</span>
              </Button>
            </PopoverTrigger>
            <PopoverContent className="w-auto">
              <div className="mb-3 flex flex-wrap gap-2">
                <Button size="sm" variant="secondary" onClick={() => applyPreset(30)}>
                  Last 30 days
                </Button>
                <Button size="sm" variant="secondary" onClick={() => applyPreset(90)}>
                  Last 90 days
                </Button>
                <Button size="sm" variant="secondary" onClick={() => applyPreset(null)}>
                  All
                </Button>
              </div>
              <Calendar
                mode="range"
                numberOfMonths={2}
                selected={selected}
                defaultMonth={selected?.from}
                disabled={{ after: new Date() }}
                onSelect={(range) =>
                  void setFilters({
                    from: range?.from ? calendarToApiDate(range.from) : null,
                    to: range?.to ? calendarToApiDate(range.to) : null,
                  })
                }
              />
            </PopoverContent>
          </Popover>
        </div>

        <div className="flex flex-col gap-2">
          <span className="text-sm font-medium" id="type-label">
            Type
          </span>
          <ToggleGroup
            type="single"
            aria-labelledby="type-label"
            value={filters.type ?? "all"}
            onValueChange={(value) =>
              value &&
              void setFilters({ type: value === "all" ? null : (value as TransactionType) })
            }
          >
            <ToggleGroupItem value="all">All</ToggleGroupItem>
            <ToggleGroupItem value="debit">Debits</ToggleGroupItem>
            <ToggleGroupItem value="credit">Credits</ToggleGroupItem>
          </ToggleGroup>
        </div>

        <div className="flex flex-1 flex-col gap-2">
          <Label htmlFor="history-search">Search</Label>
          <div className="relative">
            <Search
              aria-hidden
              className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground"
            />
            <Input
              id="history-search"
              type="search"
              placeholder="Description, counterparty or note"
              className="pl-9"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </div>
      </div>

      {active && (
        <div className="flex flex-wrap items-center gap-2" aria-label="Active filters">
          {(filters.from || filters.to) && (
            <FilterChip
              label={rangeLabel(filters.from, filters.to)}
              onRemove={() => void setFilters({ from: null, to: null })}
            />
          )}
          {filters.type && (
            <FilterChip
              label={filters.type === "debit" ? "Debits" : "Credits"}
              onRemove={() => void setFilters({ type: null })}
            />
          )}
          {filters.q && (
            <FilterChip label={`“${filters.q}”`} onRemove={() => void setFilters({ q: null })} />
          )}
          <Button variant="link" size="sm" onClick={() => void clear()}>
            Clear filters
          </Button>
        </div>
      )}
    </div>
  );
}

function FilterChip({ label, onRemove }: { label: string; onRemove: () => void }) {
  return (
    <Badge variant="outline" className="gap-1 py-1 pr-1 text-sm font-normal">
      {label}
      <button
        type="button"
        onClick={onRemove}
        aria-label={`Remove filter ${label}`}
        className="rounded-sm p-0.5 hover:bg-accent"
      >
        <X aria-hidden className="size-3.5" />
      </button>
    </Badge>
  );
}
