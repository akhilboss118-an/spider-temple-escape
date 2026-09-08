'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';

const GITHUB_APK = 'https://github.com/akhilboss118-an/spider-temple-escape/releases/latest/download/SpiderTempleEscape.apk';
const RELEASES_PAGE = 'https://github.com/akhilboss118-an/spider-temple-escape/releases';

export default function DownloadPage() {
  const [downloadStarted, setDownloadStarted] = useState(false);
  const [countdown, setCountdown] = useState(2);

  useEffect(() => {
    const timer = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          clearInterval(timer);
          triggerDownload();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, []);

  const triggerDownload = () => {
    setDownloadStarted(true);
    const link = document.createElement('a');
    link.href = GITHUB_APK;
    link.download = 'SpiderTempleEscape.apk';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div style={{
      minHeight: '100vh',
      backgroundColor: '#0a0a0f',
      color: '#f2ede8',
      fontFamily: "'Inter', sans-serif",
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      padding: '40px 20px',
      textAlign: 'center'
    }}>
      <div style={{
        maxWidth: '560px',
        width: '100%',
        backgroundColor: 'rgba(255, 255, 255, 0.04)',
        border: '1px solid rgba(212, 168, 83, 0.35)',
        borderRadius: '20px',
        padding: '36px 28px',
        boxShadow: '0 25px 60px rgba(0,0,0,0.8)'
      }}>
        <div style={{ fontSize: '48px', marginBottom: '12px' }}>🕷️</div>
        
        <h1 style={{
          fontSize: '26px',
          fontWeight: '800',
          color: '#d4a853',
          marginBottom: '8px',
          letterSpacing: '0.5px'
        }}>
          SPIDER TEMPLE ESCAPE
        </h1>

        <p style={{ color: '#8a8a9a', fontSize: '14px', marginBottom: '24px' }}>
          {countdown > 0 
            ? `Your download will begin automatically in ${countdown}s...` 
            : 'Your Android APK download has started!'}
        </p>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '28px' }}>
          <button
            onClick={triggerDownload}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '10px',
              padding: '14px 24px',
              backgroundColor: '#d4a853',
              color: '#0a0a0f',
              fontSize: '15px',
              fontWeight: '700',
              border: 'none',
              borderRadius: '12px',
              cursor: 'pointer',
              boxShadow: '0 4px 20px rgba(212, 168, 83, 0.4)'
            }}
          >
            📥 Download APK Directly (~71 MB)
          </button>

          <a
            href={RELEASES_PAGE}
            target="_blank"
            rel="noopener noreferrer"
            style={{
              padding: '12px 20px',
              backgroundColor: 'rgba(255, 255, 255, 0.08)',
              color: '#f2ede8',
              fontSize: '14px',
              fontWeight: '600',
              textDecoration: 'none',
              borderRadius: '12px',
              border: '1px solid rgba(255, 255, 255, 0.12)'
            }}
          >
            ⚡ View on GitHub Releases
          </a>
        </div>

        {/* Installation Instructions */}
        <div style={{
          backgroundColor: 'rgba(0, 0, 0, 0.35)',
          borderRadius: '14px',
          padding: '18px 20px',
          textAlign: 'left',
          fontSize: '13px',
          lineHeight: '1.7',
          color: '#b0b0c0',
          marginBottom: '24px'
        }}>
          <div style={{ color: '#2aff8a', fontWeight: '700', marginBottom: '6px' }}>
            📱 Quick Install Guide for Android:
          </div>
          <ol style={{ paddingLeft: '20px', margin: 0 }}>
            <li>Tap <b>&ldquo;Download anyway&rdquo;</b> if your browser warns about APK files.</li>
            <li>Once downloaded, tap the notification or find <code>SpiderTempleEscape.apk</code> in your Downloads folder.</li>
            <li>Enable <b>&ldquo;Install from unknown sources&rdquo;</b> in settings if prompted.</li>
            <li>Tap <b>Install</b> and launch the expedition!</li>
          </ol>
        </div>

        <div style={{ display: 'flex', justifyContent: 'center', gap: '20px', fontSize: '13px' }}>
          <Link href="/" style={{ color: '#d4a853', textDecoration: 'none', fontWeight: '600' }}>
            ← Return to Home
          </Link>
          <Link href="/#play-demo" style={{ color: '#2aff8a', textDecoration: 'none', fontWeight: '600' }}>
            ▶ Play Instant Web Demo
          </Link>
        </div>
      </div>
    </div>
  );
}
