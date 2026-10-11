#!/usr/bin/env node

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import { DatabaseSync } from "node:sqlite";

const PLATFORM_ORIGIN = "https://platforma.przeprogramowani.pl";
const DEFAULT_URL = `${PLATFORM_ORIGIN}/courses/10xdevs-4/pl`;
const MANIFEST_NAME = ".10x-lessons.json";

function usage() {
  console.log(`Usage: sync-lessons.mjs [options]

Download only missing, unlocked lesson Markdown files from the 10xDevs platform.

Options:
  --dry-run            Show new and existing lessons without writing files
  --url <course-url>   Course home or lesson URL used for discovery
  --output <dir>       Destination directory (default: ./lekcje-<course>)
  -h, --help           Show this help
`);
}

function parseArgs(argv) {
  const options = { dryRun: false, url: DEFAULT_URL, output: null };
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === "--dry-run") options.dryRun = true;
    else if (arg === "--url") options.url = argv[++index];
    else if (arg === "--output") options.output = argv[++index];
    else if (arg === "-h" || arg === "--help") options.help = true;
    else throw new Error(`Unknown argument: ${arg}`);
  }
  if (!options.url) throw new Error("--url requires a value");
  if (argv.includes("--output") && !options.output) throw new Error("--output requires a value");
  return options;
}

function parseCourseUrl(rawUrl) {
  const url = new URL(rawUrl);
  if (url.protocol !== "https:" || url.origin !== PLATFORM_ORIGIN) {
    throw new Error(`Course URL must use ${PLATFORM_ORIGIN}`);
  }
  const match = url.pathname.match(/^\/courses\/([^/]+)\/([^/]+)(?:\/([^/?#]+))?\/?$/i);
  if (!match || match[3] === "quiz" || match[3] === "checklists") {
    throw new Error("Expected a course home or lesson URL");
  }
  return {
    url,
    courseId: match[1],
    language: match[2],
    lessonRef: match[3] ? decodeURIComponent(match[3]) : null,
    basePath: `/courses/${match[1]}/${match[2]}`,
  };
}

function profileRoots() {
  const home = os.homedir();
  return [
    path.join(home, ".var", "app", "app.zen_browser.zen", ".zen"),
    path.join(home, ".zen"),
  ];
}

function candidateCookieDatabases() {
  const candidates = [];
  const explicit = process.env.ZEN_PROFILE;
  if (explicit) {
    const resolved = path.resolve(explicit);
    candidates.push(resolved.endsWith("cookies.sqlite") ? resolved : path.join(resolved, "cookies.sqlite"));
  }

  for (const root of profileRoots()) {
    if (!fs.existsSync(root)) continue;
    const iniPath = path.join(root, "profiles.ini");
    if (fs.existsSync(iniPath)) {
      const ini = fs.readFileSync(iniPath, "utf8");
      for (const section of ini.split(/^\s*\[/m).slice(1)) {
        const body = section.replace(/^[^\]]*\]\s*/, "");
        const profilePath = body.match(/^Path=(.+)$/m)?.[1]?.trim();
        if (!profilePath) continue;
        const isRelative = body.match(/^IsRelative=(.+)$/m)?.[1]?.trim() !== "0";
        const resolved = isRelative ? path.join(root, profilePath) : profilePath;
        candidates.push(path.join(resolved, "cookies.sqlite"));
      }
    }
    for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
      if (entry.isDirectory()) candidates.push(path.join(root, entry.name, "cookies.sqlite"));
    }
  }

  return [...new Set(candidates)].filter((candidate) => fs.existsSync(candidate));
}

function readPlatformToken(databasePath) {
  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), "zen-cookie-copy-"));
  const copied = path.join(tempDir, "cookies.sqlite");
  try {
    for (const suffix of ["", "-wal", "-shm"]) {
      const source = databasePath + suffix;
      if (fs.existsSync(source)) fs.copyFileSync(source, copied + suffix);
    }
    const database = new DatabaseSync(copied, { readOnly: true });
    try {
      return database.prepare(`
        SELECT value
        FROM moz_cookies
        WHERE host LIKE ? AND name = ? AND expiry > ?
        ORDER BY expiry DESC
        LIMIT 1
      `).get("%platforma.przeprogramowani.pl", "token", Math.floor(Date.now() / 1000))?.value ?? null;
    } finally {
      database.close();
    }
  } finally {
    fs.rmSync(tempDir, { recursive: true, force: true });
  }
}

function findPlatformToken() {
  const databases = candidateCookieDatabases();
  if (!databases.length) throw new Error("Could not find a Zen profile with cookies.sqlite");
  for (const database of databases) {
    try {
      const token = readPlatformToken(database);
      if (token) return token;
    } catch {
      // Another profile may be active. Never print cookie database details.
    }
  }
  throw new Error("No active platform session found in Zen. Log in and retry.");
}

function decodeHtml(value) {
  return value
    .replaceAll("&quot;", '"')
    .replaceAll("&#39;", "'")
    .replaceAll("&amp;", "&")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">");
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function discoverUnlockedLessons(html, course) {
  const serialized = html.replaceAll("&quot;", '"');
  const structuredPattern = /"locked":\[0,false\][\s\S]{0,700}?"id":\[0,"([0-9a-f-]{36})"\][\s\S]{0,700}?"name":\[0,"([^"]+)"\]/gi;
  const structured = [];
  const structuredSeen = new Set();
  for (const match of serialized.matchAll(structuredPattern)) {
    if (structuredSeen.has(match[1])) continue;
    structuredSeen.add(match[1]);
    structured.push({ ref: match[1], title: decodeHtml(match[2]) });
  }
  if (structured.length) return structured;

  const directPattern = new RegExp(`<a\\s+[^>]*href="${escapeRegex(course.basePath)}\\/([^"/?#]+)"[^>]*>([\\s\\S]*?)<\\/a>`, "gi");
  const direct = [];
  const directSeen = new Set();
  for (const match of html.matchAll(directPattern)) {
    const ref = decodeURIComponent(match[1]);
    if (directSeen.has(ref) || ref === "quiz" || ref === "checklists") continue;
    directSeen.add(ref);
    const labels = [...match[2].matchAll(/<span[^>]*>([^<]+)<\/span>/gi)]
      .map((item) => decodeHtml(item[1]).trim())
      .filter((item) => item && !/^\d+$/.test(item));
    direct.push({ ref, title: labels.at(-1) ?? ref });
  }
  if (!direct.length) throw new Error("Could not discover unlocked lessons in the authenticated course page");
  return direct;
}

function titleFromLessonHtml(html, fallback) {
  const heading = html.match(/<h1[^>]*>([\s\S]*?)<\/h1>/i)?.[1];
  if (!heading) return fallback;
  return decodeHtml(heading.replace(/<[^>]+>/g, "").trim()) || fallback;
}

function discoverMarkdownUrl(html, course, lessonRef) {
  const match = html.match(/href="([^"]+\/markdown\?filename=[^"]+)"/i);
  if (!match) throw new Error("lesson page does not contain a Markdown download link");
  const url = new URL(decodeHtml(match[1]), PLATFORM_ORIGIN);
  const expectedPath = `${course.basePath}/${encodeURIComponent(lessonRef)}/markdown`;
  if (url.origin !== PLATFORM_ORIGIN || url.pathname !== expectedPath) {
    throw new Error("lesson page returned an unexpected Markdown URL");
  }
  return url;
}

function safeFilename(value) {
  const normalized = value
    .replaceAll("ł", "l")
    .replaceAll("Ł", "L")
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-zA-Z0-9._-]+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");
  if (!normalized) throw new Error("platform returned an empty Markdown filename");
  return normalized.toLowerCase().endsWith(".md") ? normalized : `${normalized}.md`;
}

function filenameFromUrl(url) {
  return safeFilename(url.searchParams.get("filename") || "lesson");
}

function existingFilename(outputDir, serverName) {
  if (!fs.existsSync(outputDir)) return null;
  const matches = fs.readdirSync(outputDir, { withFileTypes: true })
    .filter((entry) => entry.isFile() && entry.name.toLowerCase().endsWith(".md"))
    .map((entry) => entry.name)
    .filter((name) => name === serverName || name.replace(/^\d{2,3}-/, "") === serverName)
    .sort();
  if (matches.length > 1) throw new Error(`Multiple existing files match ${serverName}; resolve the duplicate before syncing`);
  return matches[0] ?? null;
}

function readManifest(manifestPath, course) {
  if (!fs.existsSync(manifestPath)) {
    return { version: 1, courseId: course.courseId, language: course.language, lessons: {} };
  }
  const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
  if (manifest?.version !== 1 || typeof manifest.lessons !== "object" || manifest.lessons === null) {
    throw new Error(`${MANIFEST_NAME} has an unsupported format`);
  }
  if (manifest.courseId !== course.courseId || manifest.language !== course.language) {
    throw new Error(`${MANIFEST_NAME} belongs to another course or language`);
  }
  return manifest;
}

function trackedFile(outputDir, manifestEntry) {
  if (!manifestEntry || typeof manifestEntry.file !== "string") return null;
  if (path.basename(manifestEntry.file) !== manifestEntry.file) return null;
  const candidate = path.join(outputDir, manifestEntry.file);
  return fs.existsSync(candidate) && fs.statSync(candidate).isFile() ? manifestEntry.file : null;
}

async function fetchAuthenticated(url, token, expected = "page") {
  const response = await fetch(url, {
    headers: {
      cookie: `token=${token}`,
      accept: expected === "markdown" ? "text/markdown" : "text/html",
    },
    redirect: "manual",
  });
  if (response.status >= 300 && response.status < 400) {
    throw new Error("Zen session is not accepted by the platform. Log in and retry.");
  }
  if (response.status !== 200) throw new Error(`platform returned HTTP ${response.status}`);
  const contentType = response.headers.get("content-type")?.toLowerCase() ?? "";
  if (expected === "markdown" && !contentType.includes("markdown")) {
    throw new Error(`expected Markdown but received ${contentType || "an unknown content type"}`);
  }
  return response.text();
}

function writeManifest(manifestPath, manifest) {
  const temporary = path.join(path.dirname(manifestPath), `.${MANIFEST_NAME}.${process.pid}.tmp`);
  fs.writeFileSync(temporary, `${JSON.stringify(manifest, null, 2)}\n`, { encoding: "utf8", flag: "wx", mode: 0o600 });
  fs.renameSync(temporary, manifestPath);
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  if (options.help) return usage();

  const course = parseCourseUrl(options.url);
  const outputDir = path.resolve(options.output ?? `lekcje-${course.courseId}`);
  const manifestPath = path.join(outputDir, MANIFEST_NAME);
  const manifest = readManifest(manifestPath, course);
  const token = findPlatformToken();
  const startHtml = await fetchAuthenticated(course.url, token);
  const lessons = discoverUnlockedLessons(startHtml, course);
  const summary = { downloaded: [], existing: [], failed: [] };

  for (let index = 0; index < lessons.length; index += 1) {
    const lesson = lessons[index];
    try {
      const tracked = trackedFile(outputDir, manifest.lessons[lesson.ref]);
      if (tracked) {
        summary.existing.push({ title: lesson.title, file: tracked, reason: "manifest" });
        continue;
      }

      const lessonUrl = new URL(`${course.basePath}/${encodeURIComponent(lesson.ref)}`, PLATFORM_ORIGIN);
      const lessonHtml = lesson.ref === course.lessonRef ? startHtml : await fetchAuthenticated(lessonUrl, token);
      lesson.title = titleFromLessonHtml(lessonHtml, lesson.title);
      const markdownUrl = discoverMarkdownUrl(lessonHtml, course, lesson.ref);
      const serverName = filenameFromUrl(markdownUrl);
      const existing = existingFilename(outputDir, serverName);
      if (existing) {
        manifest.lessons[lesson.ref] = { title: lesson.title, file: existing, discoveredAt: new Date().toISOString() };
        summary.existing.push({ title: lesson.title, file: existing, reason: "filename" });
        continue;
      }

      const prefix = String(index).padStart(2, "0");
      const filename = `${prefix}-${serverName}`;
      if (options.dryRun) {
        summary.downloaded.push({ title: lesson.title, file: filename, preview: true });
        continue;
      }

      const markdown = await fetchAuthenticated(markdownUrl, token, "markdown");
      if (!markdown.trim() || /<!doctype html|<html/i.test(markdown)) {
        throw new Error("downloaded content is empty or HTML instead of Markdown");
      }
      fs.mkdirSync(outputDir, { recursive: true });
      const target = path.join(outputDir, filename);
      fs.writeFileSync(target, markdown, { encoding: "utf8", flag: "wx" });
      manifest.lessons[lesson.ref] = { title: lesson.title, file: filename, downloadedAt: new Date().toISOString() };
      summary.downloaded.push({ title: lesson.title, file: filename, bytes: Buffer.byteLength(markdown) });
    } catch (error) {
      summary.failed.push({ title: lesson.title, error: error instanceof Error ? error.message : String(error) });
    }
  }

  if (!options.dryRun) {
    fs.mkdirSync(outputDir, { recursive: true });
    manifest.courseUrl = `${PLATFORM_ORIGIN}${course.basePath}`;
    manifest.updatedAt = new Date().toISOString();
    writeManifest(manifestPath, manifest);
  }

  console.log(`${options.dryRun ? "Preview" : "Sync"}: ${outputDir}`);
  for (const item of summary.downloaded) console.log(`  ${item.preview ? "[new]" : "[downloaded]"} ${item.file} — ${item.title}`);
  for (const item of summary.existing) console.log(`  [existing] ${item.file} — ${item.title}`);
  for (const item of summary.failed) console.error(`  [failed] ${item.title} — ${item.error}`);
  console.log(`Summary: ${summary.downloaded.length} new, ${summary.existing.length} existing, ${summary.failed.length} failed`);
  if (summary.failed.length) process.exitCode = 1;
}

main().catch((error) => {
  console.error(`Error: ${error instanceof Error ? error.message : String(error)}`);
  process.exitCode = 1;
});
