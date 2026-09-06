'use client';

import { useEffect, useRef, useState } from 'react';
import Image from 'next/image';

// ──────────────────────────────────────────────
//  Links
// ──────────────────────────────────────────────
const GITHUB_URL  = 'https://github.com/akhilboss118-an/spider-temple-escape';
const APK_URL     = 'https://github.com/akhilboss118-an/spider-temple-escape/releases/latest/download/SpiderTempleEscape.apk';

// ──────────────────────────────────────────────
//  Data
// ──────────────────────────────────────────────
const FEATURES = [
  {
    icon: '🕷️',
    name: 'Play as Spider-Man',
    desc: 'Step into the shoes of Spider-Man! Use swift superhero reflexes to jump, slide, and weave past temple traps.',
  },
  {
    icon: '🧟',
    name: 'Relentless Zombie Monster',
    desc: 'A horrifying zombie beast pursues you with menacing speed. As your score climbs, the monster closes the gap!',
  },
  {
    icon: '⚡',
    name: 'Power-Up System',
    desc: 'Speed boosts, shields, coin magnets and invincibility — strategic power-ups to keep you ahead of the zombie.',
  },
  {
    icon: '🌿',
    name: 'Cinematic Jungle Temple',
    desc: 'Lush jungle ruins with real-time lighting, atmospheric mist, and ancient stone pathways.',
  },
  {
    icon: '🎯',
    name: 'Precision Controls',
    desc: 'Tap to jump, swipe to slide — responsive swipe-and-tilt mechanics honed for survival.',
  },
  {
    icon: '🏆',
    name: 'Score & Coin Multipliers',
    desc: 'Distance-based scoring with coin bonuses. Push your reflexes to set the ultimate high score.',
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
  return (
    <>
      {/* ── NAV ── */}
      <nav className="nav">
        <div className="container nav-inner">
          <a href="#" className="nav-logo">
            Spider<span>Temple</span>
          </a>
          <ul className="nav-links">
            <li><a href="#features">Features</a></li>
            <li><a href="#screenshots">Screenshots</a></li>
            <li><a href="#download">Download</a></li>
            <li><a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">GitHub</a></li>
          </ul>
          <a className="nav-cta" href={APK_URL} download>
            ↓ Download APK
          </a>
        </div>
      </nav>

      {/* ── HERO ── */}
      <section className="hero" id="hero">
        <div className="hero-bg" />
        <div className="hero-overlay" />

        <div className="container hero-content">
          <div className="hero-badge">Play as Spider-Man · Live on Android</div>

          <h1 className="hero-title">
            <span className="gold">Spider</span>
            <span className="line2">Temple<br />Escape</span>
          </h1>

          <p className="hero-subtitle">
            Take control of Spider-Man as you sprint through ancient jungle ruins, leap over fallen
            logs, slide beneath crumbling barriers, and outrun the terrifying Zombie monster chasing you down!
          </p>

          <div className="hero-actions">
            <a className="btn-primary" href={APK_URL} download>
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z"/>
              </svg>
              Download Free APK
            </a>
            <a className="btn-secondary" href={GITHUB_URL} target="_blank" rel="noopener noreferrer">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
              </svg>
              View on GitHub
            </a>
          </div>

          <div className="hero-stats">
            <div className="stat">
              <span className="stat-number"><Counter target={68} suffix="MB" /></span>
              <span className="stat-label">APK Size</span>
            </div>
            <div className="stat">
              <span className="stat-number"><Counter target={6} suffix="+" /></span>
              <span className="stat-label">Power-Ups</span>
            </div>
            <div className="stat">
              <span className="stat-number"><Counter target={100} suffix="%" /></span>
              <span className="stat-label">Free</span>
            </div>
          </div>
        </div>

        <div className="hero-scroll">
          <div className="scroll-line" />
          <span>Scroll</span>
        </div>
      </section>

      {/* ── FEATURES ── */}
      <section className="features" id="features">
        <div className="container">
          <p className="section-label">What awaits you</p>
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
          <div className="download-badge">Spider-Man vs Zombie Monster</div>
          <h2 className="download-title">Ready to run?</h2>
          <p className="download-sub">
            Download the APK directly — take control of Spider-Man, outrun the terrifying Zombie monster, and claim your high score! Free on Android.
          </p>

          <div className="download-buttons">
            <a className="download-apk" href={APK_URL} download>
              <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z"/>
              </svg>
              Download APK — Free
            </a>
            <a
              className="download-github"
              href={GITHUB_URL}
              target="_blank"
              rel="noopener noreferrer"
            >
              <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
              </svg>
              Source on GitHub
            </a>
          </div>

          <div className="download-meta">
            <div className="meta-chip"><span>✓</span> Android 5.0+</div>
            <div className="meta-chip"><span>✓</span> 68 MB</div>
            <div className="meta-chip"><span>✓</span> No ads · No in-app purchases</div>
            <div className="meta-chip"><span>✓</span> Built with Unity 6</div>
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
