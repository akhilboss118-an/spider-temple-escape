'use client';

import { useEffect, useRef, useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';

// ──────────────────────────────────────────────
//  Links
// ──────────────────────────────────────────────
const GITHUB_URL   = 'https://github.com/akhilboss118-an/spider-temple-escape';
const RELEASES_URL = 'https://github.com/akhilboss118-an/spider-temple-escape/releases';
const APK_URL      = '/download';
const DIRECT_APK   = '/downloads/SpiderTempleEscape.apk';

// ──────────────────────────────────────────────
//  Data
// ──────────────────────────────────────────────
const FEATURES = [
  {
    icon: '🕷️',
    name: '4 Unique Hero Suits',
    desc: 'Unlock and equip Classic Spider, Symbiote Shadow (+35% Magnet), Iron Spider (2-Hit Shield), and Cyber 2099 (2x Multiplier).',
  },
  {
    icon: '🎵',
    name: 'Dynamic Adaptive Audio',
    desc: 'Speed-scaled adrenaline jungle soundtrack, 5-note ascending chime arpeggios on heart pickups, and punchy movement SFX.',
  },
  {
    icon: '⚡',
    name: 'Relic Upgrades Shop',
    desc: 'Spend collected hearts to power up your Shield durability, Speedrun boost duration, Magnet pull radius, and Max Lives.',
  },
  {
    icon: '🧟',
    name: 'Relentless Beast Guardian',
    desc: 'A terrifying jungle monster pursues closely behind. One stumble brings it to your neck; two stumbles trigger its strike!',
  },
  {
    icon: '🌋',
    name: '3 Dynamic Biomes & Visual Polish',
    desc: 'Sprint from lush Jungle Canopy into ancient Sunken Temple Ruins and fiery Volcanic Caverns with dynamic speed FOV & wind streaks.',
  },
  {
    icon: '🌐',
    name: 'Instant WebGL Browser Play',
    desc: 'Jump right into the expedition directly from your web browser with responsive keyboard and touch swipe controls.',
  },
];

const SCREENSHOTS = [
  { src: '/ss1.jpg', label: 'Spider-Man leaping over obstacles' },
  { src: '/ss2.jpg', label: 'Zombie monster closing in' },
];

// ──────────────────────────────────────────────
//  Animated counter
// ──────────────────────────────────────────────
function Counter({ target, suffix = '' }) {
  const [count, setCount] = useState(0);
  const ref = useRef(null);
  const started = useRef(false);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !started.current) {
          started.current = true;
          let start = 0;
          const duration = 1600;
          const step = target / (duration / 16);
          const timer = setInterval(() => {
            start += step;
            if (start >= target) { setCount(target); clearInterval(timer); }
            else setCount(Math.floor(start));
          }, 16);
        }
      },
      { threshold: 0.5 }
    );
    if (ref.current) observer.observe(ref.current);
    return () => observer.disconnect();
  }, [target]);

  return <span ref={ref}>{count.toLocaleString()}{suffix}</span>;
}

// ──────────────────────────────────────────────
//  Page
// ──────────────────────────────────────────────
export default function Home() {
  const [isDemoFullscreen, setIsDemoFullscreen] = useState(false);
  const [isGamePlaying, setIsGamePlaying] = useState(false);

  return (
    <>
      {/* ── NAV ── */}
      <nav className="nav">
        <div className="container nav-inner">
          <a href="#" className="nav-logo">
            Spider<span>Temple</span>
          </a>
          <ul className="nav-links">
            <li><a href="#play-demo" className="nav-highlight">▶ Play Demo</a></li>
            <li><a href="#features">Features</a></li>
            <li><a href="#screenshots">Screenshots</a></li>
            <li><a href="#download">Download</a></li>
            <li><a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">GitHub</a></li>
          </ul>
          <Link className="nav-cta" href="/download">
            ↓ Download APK v1.2
          </Link>
        </div>
      </nav>

      {/* ── HERO ── */}
      <section className="hero" id="hero">
        <div className="hero-bg" />
        <div className="hero-overlay" />

        <div className="container hero-content">
          <div className="hero-badge">v1.2 Release · 3D Torch Braziers · Sunny Sky · 4 Hero Suits · WebGL Demo</div>

          <h1 className="hero-title">
            <span className="gold">Spider</span>
            <span className="line2">Temple<br />Escape</span>
          </h1>

          <p className="hero-subtitle">
            Take control of Spider-Man in ancient ruins! Leap over logs, slide beneath branches,
            switch lanes, unlock superhero suits, and outrun the relentless beast guardian right in your browser or on Android.
          </p>

          <div className="hero-actions">
            <a className="btn-accent" href="#play-demo" onClick={() => setIsGamePlaying(true)}>
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M8 5v14l11-7z"/>
              </svg>
              Play Instant Web Demo
            </a>
            <Link className="btn-primary" href="/download">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z"/>
              </svg>
              Download APK v1.2
            </Link>
            <a className="btn-secondary" href={GITHUB_URL} target="_blank" rel="noopener noreferrer">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
              </svg>
              GitHub
            </a>
          </div>

          <div className="hero-stats">
            <div className="stat">
              <span className="stat-number"><Counter target={4} suffix=" Suits" /></span>
              <span className="stat-label">Hero Locker</span>
            </div>
            <div className="stat">
              <span className="stat-number"><Counter target={60} suffix=" FPS" /></span>
              <span className="stat-label">Silky Physics</span>
            </div>
            <div className="stat">
              <span className="stat-number"><Counter target={100} suffix="%" /></span>
              <span className="stat-label">Free & Open Source</span>
            </div>
          </div>
        </div>

        <div className="hero-scroll">
          <div className="scroll-line" />
          <span>Scroll</span>
        </div>
      </section>

      {/* ── PLAYABLE WEB DEMO SECTION ── */}
      <section className="webgl-section" id="play-demo">
        <div className="container">
          <div className="webgl-header">
            <p className="section-label">Browser Playable Demo</p>
            <h2 className="section-title">Play in your browser</h2>
            <p className="webgl-sub">
              Experience the adrenaline rush directly inside your web browser. Jump, slide, corner 90° turns, and test out the new audio & suits!
            </p>
          </div>

          <div className={`webgl-arcade-container ${isDemoFullscreen ? 'fullscreen' : ''}`}>
            <div className="arcade-top-bar">
              <div className="arcade-lights">
                <span className="dot red" />
                <span className="dot yellow" />
                <span className="dot green" />
              </div>
              <span className="arcade-title">🕷️ SPIDER TEMPLE ESCAPE — WEBGL 60FPS</span>
              <button
                className="fullscreen-btn"
                onClick={() => setIsDemoFullscreen(!isDemoFullscreen)}
                title="Toggle Fullscreen"
              >
                {isDemoFullscreen ? '✕ Exit Fullscreen' : '⛶ Fullscreen'}
              </button>
            </div>

            <div className="arcade-screen">
              {!isGamePlaying ? (
                <div className="arcade-splash">
                  <div className="splash-icon">🕷️</div>
                  <h3 className="splash-title">Ready for the Jungle?</h3>
                  <p className="splash-desc">
                    High-octane endless runner compiled to WebAssembly. Click below to load the expedition!
                  </p>
                  <button className="btn-accent launch-btn" onClick={() => setIsGamePlaying(true)}>
                    ▶ Launch Web Demo
                  </button>
                  <div className="controls-preview">
                    <span><b>W / ↑ / Space:</b> Leap</span>
                    <span><b>S / ↓:</b> Crouch Slide</span>
                    <span><b>A / D / ← / →:</b> Lanes & Corners</span>
                  </div>
                </div>
              ) : (
                <iframe
                  src="/game/index.html"
                  className="webgl-iframe"
                  allow="autoplay; fullscreen"
                  title="Spider Temple Escape Game"
                />
              )}
            </div>

            <div className="arcade-bottom-bar">
              <div className="control-key-pill"><kbd>W</kbd> / <kbd>↑</kbd> Leap</div>
              <div className="control-key-pill"><kbd>S</kbd> / <kbd>↓</kbd> Slide</div>
              <div className="control-key-pill"><kbd>A</kbd> <kbd>D</kbd> Turn / Steer</div>
              <div className="control-key-pill"><kbd>Mouse Drag</kbd> Swipe</div>
            </div>
          </div>
        </div>
      </section>

      {/* ── FEATURES ── */}
      <section className="features" id="features">
        <div className="container">
          <p className="section-label">New & Upgraded</p>
          <h2 className="section-title">Built for<br />pure adrenaline</h2>

          <div className="features-grid">
            {FEATURES.map((f) => (
              <div key={f.name} className="feature-card">
                <span className="feature-icon">{f.icon}</span>
                <h3 className="feature-name">{f.name}</h3>
                <p className="feature-desc">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── SCREENSHOTS ── */}
      <section className="screenshots" id="screenshots">
        <div className="container">
          <div className="screenshots-header">
            <p className="section-label">In-game</p>
            <h2 className="section-title">See it in action</h2>
          </div>

          <div className="screenshots-track">
            {SCREENSHOTS.map((s) => (
              <div key={s.src} className="screenshot-item">
                <Image
                  src={s.src}
                  alt={s.label}
                  width={520}
                  height={693}
                  style={{ width: '100%', height: 'auto' }}
                />
                <div className="screenshot-label">{s.label}</div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── DOWNLOAD ── */}
      <section className="download" id="download">
        <div className="download-glow" />
        <div className="container download-inner">
          <div className="download-badge">Native Android APK v1.2 · Silky 60 FPS</div>
          <h2 className="download-title">Ready for maximum performance?</h2>
          <p className="download-sub">
            Download the native APK v1.2 directly to your Android device for the most responsive touch controls, vibration haptics, and locked 60 FPS gameplay.
          </p>

          <div className="download-buttons">
            <Link className="download-apk" href="/download">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z"/>
              </svg>
              Download APK v1.2 — Free (~71 MB)
            </Link>
            <a
              className="download-github"
              href={RELEASES_URL}
              target="_blank"
              rel="noopener noreferrer"
            >
              <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
              </svg>
              GitHub Releases
            </a>
          </div>

          <div style={{ marginTop: '16px', textAlign: 'center' }}>
            <Link
              href="/download"
              style={{
                color: '#d4a853',
                fontSize: '13px',
                fontWeight: '600',
                textDecoration: 'none',
                opacity: 0.9,
                letterSpacing: '0.2px'
              }}
            >
              📱 Need help installing? Open Android Installation Guide & Mirror →
            </Link>
          </div>

          <div className="download-meta">
            <div className="meta-chip"><span>✓</span> Android 8.0+ (ARM64 & ARMv7)</div>
            <div className="meta-chip"><span>✓</span> 4 Hero Suits Included</div>
            <div className="meta-chip"><span>✓</span> Zero In-App Ads</div>
            <div className="meta-chip"><span>✓</span> Built with Unity 6 & IL2CPP</div>
          </div>
        </div>
      </section>

      {/* ── FOOTER ── */}
      <footer className="footer">
        <div className="container footer-inner">
          <div className="footer-logo">
            Spider<span>Temple</span> Escape
          </div>

          <ul className="footer-links">
            <li><a href="#hero">Home</a></li>
            <li><a href="#play-demo">Play Demo</a></li>
            <li><a href="#features">Features</a></li>
            <li><a href="#screenshots">Screenshots</a></li>
            <li><a href="#download">Download</a></li>
            <li><a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">GitHub</a></li>
          </ul>

          <div className="footer-divider" />

          <p className="footer-copy">
            © 2025 Spider Temple Escape. Built with Unity 6 & Next.js. &nbsp;
            <a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">Open Source on GitHub</a>
          </p>
        </div>
      </footer>
    </>
  );
}
