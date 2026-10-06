"use client";

import { useEffect, useState } from "react";
import type { Role } from "@/lib/domain";

export interface Me {
  id: string;
  name: string;
  roles: Role[];
}

/** Who's signed in (for showing/hiding actions - the API is what actually enforces roles). */
export function useMe(): Me | null {
  const [me, setMe] = useState<Me | null>(null);
  useEffect(() => {
    fetch("/api/auth/session")
      .then((r) => (r.ok ? r.json() : null))
      .then(setMe)
      .catch(() => setMe(null));
  }, []);
  return me;
}

export function can(me: Me | null, ...roles: Role[]): boolean {
  return !!me && (me.roles.includes("admin") || me.roles.some((r) => roles.includes(r)));
}
