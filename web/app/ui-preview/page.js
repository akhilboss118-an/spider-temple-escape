'use client';

import Link from 'next/link';
import { useState } from 'react';

export default function UIPreview() {
  const [activeTab, setActiveTab] = useState('suite');

  return (
    <div style={{ minHeight: '100vh', background: '#070a0b', color: '#e1e3e4', display: 'flex', flexDirection: 'column' }}>
      {/* Navigation Header */}
      <header style={{
        padding: '12px 24px',
        borderBottom: '1px solid rgba(212, 175, 55, 0.25)',
        background: 'rgba(12, 15, 16, 0.95)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '12px',
        zIndex: 50
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <Link href="/" style={{ color: '#f2ca50', textDecoration: 'none', fontWeight: 'bold', fontSize: '18px' }}>
            ← Spider Temple Escape
          </Link>
          <span style={{ color: '#99907c' }}>/</span>
          <span style={{ color: '#00e478', fontSize: '12px', fontWeight: 'bold', letterSpacing: '0.1em' }}>
            STITCH UI/UX DESIGN SUITE
          </span>
        </div>

        {/* Sub-view switches */}
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            onClick={() => setActiveTab('suite')}
            style={{
              padding: '6px 14px',
              borderRadius: '8px',
              border: '1px solid #d4af37',
              background: activeTab === 'suite' ? '#f2ca50' : 'transparent',
              color: activeTab === 'suite' ? '#111415' : '#e1e3e4',
              fontWeight: 'bold',
              cursor: 'pointer',
              fontSize: '13px'
            }}
          >
            🎮 Interactive Prototype
          </button>
          <button
            onClick={() => setActiveTab('screens')}
            style={{
              padding: '6px 14px',
              borderRadius: '8px',
              border: '1px solid #d4af37',
              background: activeTab === 'screens' ? '#f2ca50' : 'transparent',
              color: activeTab === 'screens' ? '#111415' : '#e1e3e4',
              fontWeight: 'bold',
              cursor: 'pointer',
              fontSize: '13px'
            }}
          >
            📱 Standalone Stitch Screens
          </button>
          <a
            href="/stitch/index.html"
            target="_blank"
            rel="noopener noreferrer"
            style={{
              padding: '6px 14px',
              borderRadius: '8px',
              border: '1px solid #00e478',
              background: 'rgba(0, 228, 120, 0.15)',
              color: '#60ff98',
              fontWeight: 'bold',
              textDecoration: 'none',
              fontSize: '13px',
              display: 'flex',
              alignItems: 'center',
              gap: '4px'
            }}
          >
            ↗ Open Standalone
          </a>
        </div>
      </header>

      {/* Main View Area */}
      <main style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
        {activeTab === 'suite' ? (
          <iframe
            src="/stitch/index.html"
            style={{ width: '100%', flex: 1, minHeight: 'calc(100vh - 65px)', border: 'none' }}
            title="Spider Temple Escape Stitch UI/UX Interactive Prototype"
          />
        ) : (
          <div style={{ padding: '32px 16px', maxWidth: '1200px', margin: '0 auto', width: '100%' }}>
            <h2 style={{ color: '#f2ca50', fontSize: '28px', marginBottom: '8px', fontFamily: 'serif' }}>
              Generated Stitch Design Screens
            </h2>
            <p style={{ color: '#d0c5af', marginBottom: '24px' }}>
              Designed using Stitch MCP for Spider Temple Escape mobile portrait game view.
            </p>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '24px' }}>
              {/* Card 1: Loading Screen */}
              <div style={{
                background: '#121719',
                border: '1px solid #7a5e12',
                borderRadius: '16px',
                padding: '16px',
                display: 'flex',
                flexDirection: 'column',
                gap: '12px'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <h3 style={{ color: '#f2ca50', fontWeight: 'bold' }}>1. Loading Screen</h3>
                  <a href="/stitch/loading_screen.html" target="_blank" rel="noreferrer" style={{ color: '#60ff98', fontSize: '12px' }}>
                    Open Raw HTML ↗
                  </a>
                </div>
                <img
                  src="/stitch/loading_screen.png"
                  alt="Stitch Loading Screen"
                  style={{ width: '100%', borderRadius: '10px', objectFit: 'contain', maxHeight: '520px', background: '#0a0d0e' }}
                />
                <p style={{ fontSize: '12px', color: '#99907c' }}>
                  Features ancient Aztec basalt corner brackets, rotating golden spider relic medallion, obsidian glowing progress bar, and rotating survival tips.
                </p>
              </div>

              {/* Card 2: Main Menu */}
              <div style={{
                background: '#121719',
                border: '1px solid #7a5e12',
                borderRadius: '16px',
                padding: '16px',
                display: 'flex',
                flexDirection: 'column',
                gap: '12px'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <h3 style={{ color: '#f2ca50', fontWeight: 'bold' }}>2. Main Menu Screen</h3>
                  <a href="/stitch/mainmenu_screen.html" target="_blank" rel="noreferrer" style={{ color: '#60ff98', fontSize: '12px' }}>
                    Open Raw HTML ↗
                  </a>
                </div>
                <img
                  src="/stitch/mainmenu_screen.png"
                  alt="Stitch Main Menu Screen"
                  style={{ width: '100%', borderRadius: '10px', objectFit: 'contain', maxHeight: '520px', background: '#0a0d0e' }}
                />
                <p style={{ fontSize: '12px', color: '#99907c' }}>
                  Top HUD currency & lives pills, equipped suit 3D preview card, radiant Start Expedition CTA, and tactile thumb cluster action cards.
                </p>
              </div>

              {/* Card 3: Death Scene */}
              <div style={{
                background: '#121719',
                border: '1px solid #7a5e12',
                borderRadius: '16px',
                padding: '16px',
                display: 'flex',
                flexDirection: 'column',
                gap: '12px'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <h3 style={{ color: '#f2ca50', fontWeight: 'bold' }}>3. Death / Defeat Scene</h3>
                  <a href="/stitch/death_screen.html" target="_blank" rel="noreferrer" style={{ color: '#60ff98', fontSize: '12px' }}>
                    Open Raw HTML ↗
                  </a>
                </div>
                <img
                  src="/stitch/death_screen.png"
                  alt="Stitch Death Screen"
                  style={{ width: '100%', borderRadius: '10px', objectFit: 'contain', maxHeight: '520px', background: '#0a0d0e' }}
                />
                <p style={{ fontSize: '12px', color: '#99907c' }}>
                  Crimson defeat vignette, cause-of-death badge, expedition metrics summary tablet (Distance, Relics, Score), Re-enter Temple, and Sanctuary CTA.
                </p>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}
