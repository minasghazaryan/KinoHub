# Kinopoisk API minimization – implementation plan

**Goal:** Use DB (and in-memory cache where it already exists) as the primary source for film data; call Kinopoisk API only when data is missing or stale.

---

## 1. Index page – catalog & collections

### Current behavior
- **Default (no search, no collection):** DB first → API fallback if DB fails.
- **Collection tabs (НОВИНКИ, СЕРИАЛЫ, ТОП 250):** Always API.
- **Anime / Мультфильмы:** Always API (filters by genre).
- **Search (?q=):** Always API.

### Target behavior

| Scenario | Primary source | Fallback / notes |
|----------|----------------|------------------|
| Default catalog (filters: genre, country, year, order) | **DB** – `GetMoviesFromDbAsync` (already) | API only if DB returns 0 and we want “fill from API once” (optional). |
| Collection tab (e.g. TOP_250_MOVIES, POPULAR_SERIES) | **DB** – new: “get movies that belong to this collection” | **API** if DB has no (or too few) items for that collection. |
| Anime / Мультфильмы (genre filter) | **DB** – `GetMoviesFromDbAsync(genreId: 24 or 18)` (already) | API only if DB empty (already as fallback). |
| Search (?q=...) | Keep **API** | Optional later: search in DB by title (e.g. full-text) and merge with API. |

### Implementation steps (Index)

1. **Define “collection” in DB (optional but useful)**  
   - Either: infer collection from existing data (e.g. “top by rating” = TOP_250, “series” = POPULAR_SERIES) and keep using current `Movies` + genres.  
   - Or: add a table e.g. `MovieCollection` (MovieId, CollectionType, DisplayOrder) filled by Admin when syncing “by collection”.  
   - **Recommendation:** Start without new table: for “collection” tabs, use **same catalog endpoint params** that Admin uses when syncing (e.g. order=RATING + type=ALL for “главная”, type=TV_SERIES for “сериалы”). So we can map `collection` → (order, type) and call `GetMoviesFromDbAsync` with that.

2. **Map collection → (order, type) for DB query**  
   - e.g. `TOP_250_MOVIES` → order=RATING, type=ALL; `POPULAR_SERIES` → order=RATING, type=TV_SERIES; `TOP_POPULAR_ALL` → order=RATING, type=ALL.  
   - When user selects a collection tab, call **DB** with these params (and optional genre/country/year from filters). No API call.

3. **When to call API on Index**  
   - Only when: (a) **Search** (?q=), or (b) **DB returns empty** and we decide to “fill once” from API (optional), or (c) **Admin** sync.  
   - Remove or narrow **GetCollectionAsync** / **GetFilmsByFiltersAsync** from Index for normal catalog and collection views.

4. **Admin**  
   - Stays as is: “Sync page” / “Sync all” fill DB from API. No change needed for Admin flow.

---

## 2. Details page – film details and related data

### Current behavior
- Film details: API every time, **in-memory cached** 10–30 min via `ApiCacheService`.
- Seasons, Videos, Staff, Facts, BoxOffice, Awards, Similars, Reviews: **API every time**, no DB cache.

### Target behavior

| Data | Primary source | Fallback | TTL / invalidation |
|------|----------------|----------|--------------------|
| Film details (title, description, poster, etc.) | **DB** – from `Movies` + optional “details blob” or extended table | **API** if not in DB or stale; then update DB | e.g. 24 h or 7 days in DB; in-memory cache 10–30 min as now. |
| Seasons | **DB** (new table or JSON blob) or **API** | API if not in DB | e.g. 24 h |
| Videos (trailers) | **DB** (new table or JSON) or **API** | API if not in DB | e.g. 24 h |
| Staff | **DB** or **API** | API if not in DB | e.g. 24 h |
| Similars | **DB** or **API** | API if not in DB | e.g. 24 h |
| Reviews | **API** or **DB** (optional) | Keep API if we don’t store reviews | Can stay API-only (reviews change often). |
| Facts / Box office / Awards | **API** or **DB** (optional) | Low priority; can stay API or add later | Optional. |

### Implementation steps (Details)

1. **Film details from DB first**  
   - **Read:** On Details load, try **DB**: get by KinopoiskId from `Movies` (and if you add it: a “details” row or blob with description, slogan, etc.).  
   - **Miss or stale:** Call **API** `GetMovieDetailsAsync`, then **write** to DB (update `Movies` and any details table/blob).  
   - **In-memory:** Keep using `ApiCacheService` for the API response (or for the merged “details” DTO) so repeated views of the same film don’t hit DB/API every time.

2. **Optional: “Details cache” table**  
   - Table e.g. `KinopoiskDetailsCache` (KinopoiskId, JsonBlob, FetchedAt).  
   - Store full film-details JSON (or a DTO) and FetchedAt.  
   - On Details load: read by KinopoiskId; if FetchedAt &gt; 24 h (or 7 days), use it; else fetch from API and update row.  
   - Reduces API calls for film details to once per film per TTL.

3. **Seasons / Videos / Staff / Similars**  
   - **Option A (simplest):** Keep calling API; add **in-memory cache** (e.g. `ApiCacheService`) per `kinopoiskId` + data type (e.g. `seasons:{id}`, `videos:{id}`) with TTL 10–30 min.  
   - **Option B (stronger):** Add DB cache table(s) or one table with (KinopoiskId, DataType, JsonBlob, FetchedAt). On Details load, read from DB; if missing or stale, call API and write to DB.  
   - Recommendation: start with **Option A** for seasons/videos/staff/similars; introduce **Option B** if you need fewer API calls.

4. **Reviews / Facts / Box office / Awards**  
   - Can remain **API-only** initially (or Reviews + Facts cached in DB with shorter TTL later).  
   - No change in first iteration if you want to ship quickly.

---

## 3. Summary of changes (by component)

| Component | Change |
|-----------|--------|
| **Index – default catalog** | Already DB first; keep as is. Optionally remove API fallback or make it “one-time fill”. |
| **Index – collection tabs** | Use **DB** with mapped (order, type) and existing genre/country/year filters; **no** `GetCollectionAsync` for these. |
| **Index – anime/mult** | Use **DB** with genreId (already supported); API only if DB empty. |
| **Index – search** | Keep **API** for now. |
| **Details – film details** | **DB first** (from `Movies` + optional details cache table); API on miss/stale; keep in-memory cache. |
| **Details – seasons, videos, staff, similars** | Add **in-memory cache** (e.g. ApiCacheService) with TTL; optionally add DB cache later. |
| **Details – reviews, etc.** | Leave **API** for now. |
| **Admin** | No change; continues to sync from API into DB. |

---

## 4. Suggested order of implementation

1. **Index: collection tabs from DB**  
   - Add mapping collection → (order, type).  
   - In Index, when `collection` is set (and not ANIME/KIDS_ANIMATION_THEME), call `GetMoviesFromDbAsync` with that order/type (and current genre/country/year) instead of `GetCollectionAsync`.  
   - Only call API when DB returns 0 (or skip and show “sync in Admin” message).

2. **Details: film details from DB**  
   - Try loading film from `Movies` by KinopoiskId; if found and “fresh enough”, use it for the main details block.  
   - If not in DB or stale, call API and update `Movies` (and optional details cache).  
   - Keep using ApiCacheService for the API-sourced (or merged) DTO so repeated visits don’t hit DB/API every time.

3. **Details: in-memory cache for seasons/videos/staff/similars**  
   - Wrap `GetSeasonsAsync`, `GetVideosAsync`, `GetStaffAsync`, `GetSimilarsAsync` in `ApiCacheService.GetOrCreateAsync` with keys like `kinopoisk:seasons:{id}`, TTL 10–30 min.  
   - No DB schema change.

4. **(Optional) DB cache for Details**  
   - Add `KinopoiskDetailsCache` (or similar) and use it for film details and/or seasons/videos/staff/similars with a 24 h (or 7 day) TTL.

---

## 5. Edge cases

- **New film:** Not in DB until Admin syncs or someone opens Details (then we fetch and save). Acceptable.
- **Quota / 402:** If API returns 402, DB/cache still serves what we have; show a short message on Details if we couldn’t refresh.
- **Stale data:** TTLs (in-memory and DB) limit staleness; Admin “Sync all” keeps catalog fresh.

This plan keeps implementation order clear and minimizes API calls while reusing existing DB and cache.
