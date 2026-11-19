import { useSyncExternalStore } from "react";

export type SetState<T> = (partial: Partial<T> | ((state: T) => Partial<T>), replace?: boolean) => void;
export type GetState<T> = () => T;
export type StoreListener<T> = (state: T, previous: T) => void;
export type StoreInitializer<T> = (set: SetState<T>, get: GetState<T>) => T;

export interface StoreApi<T> {
  getState: GetState<T>;
  setState: SetState<T>;
  subscribe: (listener: StoreListener<T>) => () => void;
}

/**
 * A store usable as a hook: `useStore()` returns the whole state, `useStore(selector)` a slice. A selector must return
 * a stable value (a field or a primitive), never a freshly built object, or React re-renders forever.
 */
export interface BoundStore<T> extends StoreApi<T> {
  (): T;
  <U>(selector: (state: T) => U): U;
}

export interface PersistOptions<T, P> {
  /** The localStorage key. */
  name: string;
  /** The part of the state worth keeping across reloads. */
  partialize: (state: T) => P;
  /** Runs once at creation with the state after stored values were applied, whether or not anything was stored. */
  onRehydrate?: (state: T) => void;
}

// The { state, version } envelope zustand's persist middleware used, so values already saved in browsers keep loading.
interface StoredEnvelope<P> {
  state: P;
  version: number;
}

const STORAGE_VERSION = 0;

const identity = <T,>(state: T): T => state;

export function createStore<T>(initializer: StoreInitializer<T>): BoundStore<T> {
  const listeners = new Set<StoreListener<T>>();
  let state: T;

  const getState: GetState<T> = () => state;

  const setState: SetState<T> = (partial, replace = false) => {
    const patch = typeof partial === "function" ? partial(state) : partial;
    const next = replace ? (patch as T) : { ...state, ...patch };
    if (Object.is(next, state)) {
      return;
    }

    const previous = state;
    state = next;
    listeners.forEach((listener) => listener(state, previous));
  };

  const subscribe = (listener: StoreListener<T>) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  };

  state = initializer(setState, getState);

  function useStore(): T;
  function useStore<U>(selector: (state: T) => U): U;
  function useStore<U>(selector: (state: T) => U = identity as (state: T) => U): U {
    return useSyncExternalStore(
      subscribe,
      () => selector(state),
      () => selector(state),
    );
  }

  return Object.assign(useStore, { getState, setState, subscribe });
}

/**
 * Like {@link createStore}, but keeps `partialize(state)` in localStorage under `name`. Storage failures (private mode,
 * blocked site data, corrupt JSON) leave the store working in memory only.
 */
export function createPersistedStore<T, P extends Partial<T>>(
  initializer: StoreInitializer<T>,
  options: PersistOptions<T, P>,
): BoundStore<T> {
  const store = createStore(initializer);

  const stored = readStorage<P>(options.name);
  if (stored) {
    store.setState(stored);
  }
  options.onRehydrate?.(store.getState());

  store.subscribe((state) => writeStorage(options.name, options.partialize(state)));
  return store;
}

function readStorage<P>(name: string): P | null {
  try {
    const raw = window.localStorage.getItem(name);
    if (!raw) {
      return null;
    }

    const envelope = JSON.parse(raw) as Partial<StoredEnvelope<P>> | null;
    return envelope && typeof envelope.state === "object" && envelope.state !== null ? envelope.state : null;
  } catch {
    return null;
  }
}

function writeStorage<P>(name: string, value: P) {
  try {
    const envelope: StoredEnvelope<P> = { state: value, version: STORAGE_VERSION };
    window.localStorage.setItem(name, JSON.stringify(envelope));
  } catch {
    // Storage unavailable or full: the in-memory state is still correct.
  }
}
