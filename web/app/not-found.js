'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';

export default function NotFound() {
  const [webs, setWebs] = useState([]);

  useEffect(() => {
    const particles = Array.from({ length: 18 }, (_, i) => ({
      id: i,
      x: Math.random() * 100,
      y: Math.random() * 100,
      size: 12 + Math.random() * 24,
      delay: Math.random() * 6,
      duration: 4 + Math.random() * 5,
      char: ['🕷', '🕸', '💀', '🏚', '🌙', '🦇'][i % 6],
    }));
    setWebs(particles);
  }, []);

  return (
    <div className="not-found">
      <div className="nf-particles">
        {webs.map((p) => (
          <span
            key={p.id}
            className="nf-particle"
            style={{
              left: `${p.x}%`,
              top: `${p.y}%`,
              fontSize: `${p.size}px`,
              animationDelay: `${p.delay}s`,
              animationDuration: `${p.duration}s`,
            }}
          >
            {p.char}
          </span>
        ))}
      </div>

      <div className="nf-content">
        <div className="nf-code">
          <span className="nf-4">4</span>
          <span className="nf-0">🕷</span>
          <span className="nf-4">4</span>
        </div>

        <h1 className="nf-title">Lost in the Temple</h1>
        <p className="nf-sub">
          This path has crumbled into the abyss. The ancient guardians have
          sealed this corridor — there is nothing beyond here.
        </p>

        <div className="nf-actions">
          <Link className="nf-btn nf-btn-primary" href="/">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
              <path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z" />
            </svg>
            Return to Safety
          </Link>
          <Link className="nf-btn nf-btn-secondary" href="/download">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z" />
            </svg>
            Download APK
          </Link>
        </div>

        <div className="nf-hint">
          <span className="nf-hint-icon">💡</span>
          <span>Tip: Double-check the URL or head back to the home page.</span>
        </div>
      </div>

      <div className="nf-spider-trail">
        {Array.from({ length: 5 }).map((_, i) => (
          <div
            key={i}
            className="nf-spider"
            style={{
              left: `${15 + i * 18}%`,
              animationDelay: `${i * 0.8}s`,
            }}
          >
            🕷
          </div>
        ))}
        <div className="nf-web-line" />
      </div>
    </div>
  );
}
