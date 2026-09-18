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
//  Characters Data (from CharacterManager.cs)
// ──────────────────────────────────────────────
const CHARACTERS = [
  {
    id: 'char_naruto',
    name: 'Naruto',
    title: 'Hokage Runner',
    avatar: '/characters/naruto.png',
    archetype: 'Shinobi Master',
    specialty: 'Speed & Reflexes',
    speed: 1.25,
    shield: 1.10,
    agility: 1.20,
    description: 'Legendary shinobi runner with lightning reflexes, high speed, and fearless agility through ancient ruins.',
  },
  {
    id: 'spiderman_classic',
    name: 'Spider-Man',
    title: 'Temple Runner',
    avatar: '/characters/spiderman.png',
    archetype: 'All-Rounder',
    specialty: 'Balanced Classic',
    speed: 1.00,
    shield: 1.00,
    agility: 1.00,
    description: 'Iconic agile web-slinger. Balanced athletic reflexes to leap over fallen jungle timber and slide beneath stone arches.',
  },
  {
    id: 'char_nezuko',
    name: 'Nezuko',
    title: 'Demon Voyager',
    avatar: '/characters/nezuko.png',
    archetype: 'Acrobat',
    specialty: 'Fluid Dodges',
    speed: 1.15,
    shield: 1.05,
    agility: 1.30,
    description: 'Agile demon voyager capable of supernatural recovery, fluid obstacle evasion, and balanced endurance.',
  },
  {
    id: 'char_hinata',
    name: 'Hinata',
    title: 'Byakugan Scout',
    avatar: '/characters/hinata.png',
    archetype: 'Scout',
    specialty: 'Max Agility (1.35x)',
    speed: 1.20,
    shield: 0.95,
    agility: 1.35,
    description: 'Fleet-footed Byakugan scout with sharp instincts, high aerial grace, and lightning navigation through treacherous paths.',
  },
  {
    id: 'char_zoro',
    name: 'Zoro',
    title: 'Ruin Swordsman',
    avatar: '/characters/zoro.png',
    archetype: 'Tank',
    specialty: 'Heavy Shield (1.30x)',
    speed: 1.10,
    shield: 1.30,
    agility: 1.00,
    description: 'Stalwart swordsman with rock-solid stability to withstand rough collisions and power through obstacles.',
  },
  {
    id: 'char_zenitsu',
    name: 'Zenitsu',
    title: 'Thunder Striker',
    avatar: '/characters/zenitsu.png',
    archetype: 'Speedster',
    specialty: 'Top Speed (1.35x)',
    speed: 1.35,
    shield: 1.00,
    agility: 1.25,
    description: 'High-velocity thunder runner possessing explosive speed bursts and aerial maneuvers.',
  },
  {
    id: 'char_anya',
    name: 'Anya',
    title: 'Secret Telepath',
    avatar: '/characters/anya.png',
    archetype: 'Infiltrator',
    specialty: 'Low Slides & Evasion',
    speed: 1.20,
    shield: 1.15,
    agility: 1.30,
    description: 'Clever acrobatic athlete specialized in swift low slides, secret instinct, and obstacle clearance.',
  },
  {
    id: 'char_sasuke',
    name: 'Sasuke',
    title: 'Shadow Avenger',
    avatar: '/characters/sasuke.png',
    archetype: 'Commander',
    specialty: 'Apex Attributes',
    speed: 1.30,
    shield: 1.20,
    agility: 1.15,
    description: 'Master shinobi explorer with masterclass attributes across speed, shield defense, and obstacle agility.',
  },
];

// ──────────────────────────────────────────────
//  Features Data
// ──────────────────────────────────────────────
const FEATURES = [
  {
    icon: '⚡',
    name: '8 Playable Temple Heroes',
    desc: 'Sprint as Spider-Man, Naruto, Nezuko, Hinata, Zoro, Zenitsu, Anya, or Sasuke — each fine-tuned with custom Speed, Shield, and Agility multipliers!',
  },
  {
    icon: '🎵',
    name: 'Dynamic Adaptive Audio',
    desc: 'Speed-scaled adrenaline jungle soundtrack, 5-note ascending chime arpeggios on heart pickups, and punchy movement SFX.',
  },
  {
    icon: '🛡️',
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
    name: '3 Dynamic Biomes & PBR Visuals',
    desc: 'Sprint from lush Jungle Canopy into ancient Sunken Temple Ruins and fiery Volcanic Caverns with PBR materials, dynamic color grading, and atmospheric fog.',
  },
  {
    icon: '🎮',
    name: 'Ultra-Smooth 60 FPS Engine',
    desc: 'Compiled natively with Unity 6 & IL2CPP for lag-free 60 FPS rendering, tactile touch swipes, and vibration haptic feedback on Android.',
  },
];

// ──────────────────────────────────────────────
//  Real In-Game Screenshots
// ──────────────────────────────────────────────
const SCREENSHOTS = [
  {
    src: '/screenshots/real_gameplay_shield_run.png',
    title: 'Energy Shield & Beast Pursuit',
    badge: 'In-Game Action · 60 FPS',
    caption: 'Naruto sprinting with active invulnerability shield while the ancient relic beast charges closely behind.',
  },
  {
    src: '/screenshots/real_gameplay_track_overview.png',
    title: 'Ancient Causeway & Relics',
    badge: 'Dynamic Track Generation',
    caption: 'Sunken stone ruins featuring jump logs, coin cascades, sacred hearts, and dense jungle canopy foliage.',
  },
  {
    src: '/screenshots/real_gameplay_slide_arch.png',
    title: 'Arched Root Slide & Magnet',
    badge: 'Obstacle Evasion',
    caption: 'Duck beneath low-hanging timber arches while activating the magnetic heart & coin pull powerup.',
  },
];

// ──────────────────────────────────────────────
//  FAQ Data
// ──────────────────────────────────────────────
const FAQ = [
  { q: 'Is Spider Temple Escape free?', a: 'Yes, 100% free with zero in-app purchases and zero ads. Download the APK and play the full game instantly.' },
  { q: 'Which Android versions are supported?', a: 'Android 8.0 (Oreo) and above. The APK includes both ARM64 and ARMv7 native libraries for maximum device compatibility.' },
  { q: 'How do I install the APK?', a: 'Download the APK, open it from your Notifications or Downloads folder, enable "Install from unknown sources" if prompted, then tap Install. The whole process takes under a minute.' },
  { q: 'What are character abilities?', a: "Each of the 8 heroes has a unique special ability that triggers automatically during gameplay — like Naruto's extended magnet radius, Zenitsu's thunder speed burst, or Nezuko's demon regeneration." },
  { q: 'How does the progressive difficulty work?', a: 'The game gets harder the further you run. After 500m, hazard density increases. At 2000m+, the monster enters Phase 2 with visual changes. Beyond 5000m, Phase 3 activates fire trails and aggressive lunges.' },
  { q: 'Does it work offline?', a: 'Absolutely. Once installed, the game runs entirely offline — no internet connection required. Perfect for commutes and travel.' },
  { q: 'Can I play as all 8 characters from the start?', a: "Yes! All 8 heroes — Spider-Man, Naruto, Nezuko, Hinata, Zoro, Zenitsu, Anya, and Sasuke — are fully unlocked and playable from your first run." },
  { q: 'What frame rate does the game run at?', a: 'Locked 60 FPS on all compatible devices. The game uses Unity 6 IL2CPP native compilation for butter-smooth physics and rendering.' },
];

// ──────────────────────────────────────────────
//  How To Play Steps
// ──────────────────────────────────────────────
const CONTROLS = [
  { gesture: '👆', label: 'Swipe Up', action: 'Jump', desc: 'Leap over fallen logs, stone barriers, and low obstacles' },
  { gesture: '👇', label: 'Swipe Down', action: 'Slide', desc: 'Duck beneath arches, branches, and overhead beams' },
  { gesture: '👈', label: 'Swipe Left', action: 'Lane Left', desc: 'Dodge into the left lane to avoid center obstacles' },
  { gesture: '👉', label: 'Swipe Right', action: 'Lane Right', desc: 'Switch to the right lane to evade incoming hazards' },
  { gesture: '⏸', label: 'Tap Screen', action: 'Power-Up', desc: 'Trigger your collected Shield, Magnet, or Speed Boost' },
];

// ──────────────────────────────────────────────
//  Parallax Floating Particles
// ──────────────────────────────────────────────
const PARALLAX_ITEMS = [
  { char: '🪙', size: 28, x: 8,  y: 15, speed: 12, delay: 0 },
  { char: '🪙', size: 22, x: 85, y: 25, speed: 15, delay: 2 },
  { char: '🕸️', size: 32, x: 12, y: 70, speed: 18, delay: 1 },
  { char: '🪙', size: 20, x: 72, y: 60, speed: 14, delay: 3 },
  { char: '🕷️', size: 26, x: 92, y: 45, speed: 16, delay: 0.5 },
  { char: '🍃', size: 18, x: 25, y: 35, speed: 20, delay: 1.5 },
  { char: '🪙', size: 16, x: 55, y: 80, speed: 13, delay: 4 },
  { char: '🕸️', size: 24, x: 38, y: 10, speed: 17, delay: 2.5 },
  { char: '🍃', size: 14, x: 65, y: 90, speed: 19, delay: 0.8 },
  { char: '🕷️', size: 20, x: 5,  y: 50, speed: 11, delay: 3.5 },
  { char: '🪙', size: 24, x: 45, y: 45, speed: 14, delay: 1.2 },
  { char: '🍃', size: 16, x: 78, y: 80, speed: 22, delay: 2.8 },
];

// ──────────────────────────────────────────────
//  Scroll Animation Hook
// ──────────────────────────────────────────────
function useScrollReveal(threshold = 0.15) {
  const ref = useRef(null);
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const obs = new IntersectionObserver(
      ([entry]) => { if (entry.isIntersecting) setVisible(true); },
      { threshold }
    );
    obs.observe(el);
    return () => obs.disconnect();
  }, [threshold]);
  return [ref, visible];
}

function RevealSection({ children, className = '', id }) {
  const [ref, visible] = useScrollReveal(0.1);
  return (
    <section id={id} ref={ref} className={`${className} reveal ${visible ? 'revealed' : ''}`}>
      {children}
    </section>
  );
}

// ──────────────────────────────────────────────
//  FAQ Accordion Item
// ──────────────────────────────────────────────
function FAQItem({ item, index, isOpen, onToggle }) {
  return (
    <div className={`faq-item ${isOpen ? 'open' : ''}`}>
      <button className="faq-question" onClick={onToggle} type="button">
        <span className="faq-index">{String(index + 1).padStart(2, '0')}</span>
        <span className="faq-q-text">{item.q}</span>
        <span className="faq-chevron">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="6 9 12 15 18 9" /></svg>
        </span>
      </button>
      <div className="faq-answer-wrap">
        <div className="faq-answer">{item.a}</div>
      </div>
    </div>
  );
}

// ──────────────────────────────────────────────
//  Character Comparison Component
// ──────────────────────────────────────────────
function CharacterCompare({ characters }) {
  const [leftIdx, setLeftIdx] = useState(0);
  const [rightIdx, setRightIdx] = useState(3);
  const left = characters[leftIdx];
  const right = characters[rightIdx];
  const statMeta = {
    speed:  { bar: 'var(--gold)',  glow: 'rgba(212,168,83,0.6)', label: '⚡' },
    shield: { bar: 'var(--green-accent)', glow: 'rgba(42,255,138,0.6)', label: '🛡️' },
    agility:{ bar: '#38bdf8', glow: 'rgba(56,189,248,0.6)', label: '🌪️' },
  };
  const maxVal = 1.5;

  return (
    <div className="compare-container">
      <div className="compare-pick-row">
        <div className="compare-pick">
          <label className="compare-label">Challenger A</label>
          <select className="compare-select" value={leftIdx} onChange={(e) => setLeftIdx(Number(e.target.value))}>
            {characters.map((c, i) => <option key={c.id} value={i}>{c.name} — {c.title}</option>)}
          </select>
        </div>
        <div className="compare-vs">VS</div>
        <div className="compare-pick">
          <label className="compare-label">Challenger B</label>
          <select className="compare-select" value={rightIdx} onChange={(e) => setRightIdx(Number(e.target.value))}>
            {characters.map((c, i) => <option key={c.id} value={i}>{c.name} — {c.title}</option>)}
          </select>
        </div>
      </div>

      <div className="compare-cards">
        {[left, right].map((hero, side) => (
          <div key={`${hero.id}-${side}`} className={`compare-card ${side === 0 ? 'left' : 'right'}`}>
            <div className="compare-card-avatar-wrap">
              <Image src={hero.avatar} alt={hero.name} width={80} height={80} className="compare-card-avatar" />
            </div>
            <h4 className="compare-card-name">{hero.name}</h4>
            <span className="compare-card-title">{hero.title}</span>
            <span className="compare-card-archetype">{hero.archetype}</span>
          </div>
        ))}
      </div>

      <div className="compare-stats">
        {Object.entries(statMeta).map(([key, cfg]) => {
          const lv = left[key], rv = right[key];
          const lw = lv > rv, rw = rv > lv, tie = lv === rv;
          return (
            <div key={key} className="compare-stat-row">
              <div className="compare-stat-side left">
                <span className={`compare-stat-val ${lw ? 'winner' : tie ? 'tie' : ''}`}>{lv.toFixed(2)}x</span>
                {lw && <span className="compare-crown">👑</span>}
              </div>
              <div className="compare-stat-center">
                <span className="compare-stat-icon">{cfg.label}</span>
                <span className="compare-stat-name">{key.charAt(0).toUpperCase() + key.slice(1)}</span>
              </div>
              <div className="compare-stat-side right">
                {rw && <span className="compare-crown">👑</span>}
                <span className={`compare-stat-val ${rw ? 'winner' : tie ? 'tie' : ''}`}>{rv.toFixed(2)}x</span>
              </div>
              <div className="compare-bar-wrap">
                <div className="compare-bar-track lt">
                  <div className="compare-bar-fill" style={{ width: `${(lv / maxVal) * 100}%`, background: cfg.bar, boxShadow: `0 0 12px ${cfg.glow}`, float: 'right' }} />
                </div>
                <div className="compare-bar-track rt">
                  <div className="compare-bar-fill" style={{ width: `${(rv / maxVal) * 100}%`, background: cfg.bar, boxShadow: `0 0 12px ${cfg.glow}` }} />
                </div>
              </div>
            </div>
          );
        })}
      </div>

      <div className="compare-verdict">
        {(() => {
          const ls = left.speed + left.shield + left.agility;
          const rs = right.speed + right.shield + right.agility;
          if (ls > rs) return <span><strong>{left.name}</strong> has the edge — total stat sum {ls.toFixed(2)} vs {rs.toFixed(2)}</span>;
          if (rs > ls) return <span><strong>{right.name}</strong> has the edge — total stat sum {rs.toFixed(2)} vs {ls.toFixed(2)}</span>;
          return <span>Perfectly matched! Both heroes share a total stat sum of {ls.toFixed(2)}</span>;
        })()}
      </div>
    </div>
  );
}

// ──────────────────────────────────────────────
//  Animated Counter
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
//  Page Component
// ──────────────────────────────────────────────
export default function Home() {
  const [selectedCharacterIndex, setSelectedCharacterIndex] = useState(0);
  const [faqOpenIndex, setFaqOpenIndex] = useState(null);

  const activeHero = CHARACTERS[selectedCharacterIndex] || CHARACTERS[0];

  return (
    <>
      {/* ── NAV ── */}
      <nav className="nav">
        <div className="container nav-inner">
          <a href="#" className="nav-logo" style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Image src="/app_logo.png" alt="Spider Temple Escape Logo" width={36} height={36} style={{ borderRadius: '8px', objectFit: 'cover', boxShadow: '0 2px 10px rgba(212,168,83,0.3)' }} />
            <span>Spider<span>Temple</span></span>
          </a>
          <ul className="nav-links">
            <li><a href="#roster">Roster</a></li>
            <li><a href="#compare">Compare</a></li>
            <li><a href="#how-to-play">How to Play</a></li>
            <li><a href="#features">Features</a></li>
            <li><a href="#screenshots">Screenshots</a></li>
            <li><a href="#faq">FAQ</a></li>
            <li><a href="#download">Download</a></li>
            <li><a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">GitHub</a></li>
          </ul>
          <Link className="nav-cta" href="/download">
            ↓ Download APK v1.7
          </Link>
        </div>
      </nav>

      {/* ── HERO ── */}
      <section className="hero" id="hero">
        <div className="hero-bg" />
        <div className="hero-overlay" />

        <div className="parallax-layer">
          {PARALLAX_ITEMS.map((p, i) => (
            <span
              key={i}
              className="parallax-particle"
              style={{
                left: `${p.x}%`,
                top: `${p.y}%`,
                fontSize: `${p.size}px`,
                animationDuration: `${p.speed}s`,
                animationDelay: `${p.delay}s`,
              }}
            >
              {p.char}
            </span>
          ))}
        </div>

        <div className="container hero-content">
          <div className="hero-badge">v1.7 Release · Character Abilities · Running Trails · Dynamic Music · Monster Roar · 60 FPS Native APK</div>

          <h1 className="hero-title">
            <span className="gold">Spider</span>
            <span className="line2">Temple<br />Escape</span>
          </h1>

          <p className="hero-subtitle">
            Take control of Spider-Man, Naruto, Nezuko, Hinata, Zoro, Zenitsu, Anya, and Sasuke in ancient ruins! Leap over logs, slide beneath branches,
            switch lanes, master individual speed & agility stats, and outrun the relentless beast guardian on Android with locked 60 FPS native physics.
          </p>

          <div className="hero-actions">
            <Link className="btn-accent" href="/download">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z"/>
              </svg>
              Download APK v1.7
            </Link>
            <a className="btn-primary" href="#roster">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z"/>
              </svg>
              Meet the 8 Heroes
            </a>
            <a className="btn-secondary" href={GITHUB_URL} target="_blank" rel="noopener noreferrer">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
              </svg>
              GitHub
            </a>
          </div>

          <div className="hero-stats">
            <div className="stat">
              <span className="stat-number"><Counter target={8} suffix=" Heroes" /></span>
              <span className="stat-label">Playable Roster</span>
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

      {/* ── 8-CHARACTER ROSTER SHOWCASE ── */}
      <section className="roster-section" id="roster">
        <div className="container">
          <div className="roster-header">
            <p className="section-label">Playable Heroes</p>
            <h2 className="section-title">Select Your Temple Runner</h2>
            <p className="roster-sub">
              All 8 legendary runners are fully unlocked and ready for the expedition! Each hero features unique attributes for Speed, Shield Durability, and Obstacle Agility calibrated in CharacterManager.
            </p>
          </div>

          {/* Arcade Roster Banner */}
          <div className="roster-arcade-banner">
            <div className="roster-banner-header">
              <div className="arcade-lights">
                <span className="dot red" />
                <span className="dot yellow" />
                <span className="dot green" />
              </div>
              <span>🕹️ 90s ARCADE CHARACTER ROSTER · SPIDER-MAN & 7 ANIME HEROES</span>
              <span>TEMPLE EDITION</span>
            </div>
            <div className="roster-banner-img-wrap">
              <Image
                src="/character_select_roster.jpg"
                alt="Arcade Character Select Roster"
                width={1280}
                height={720}
                style={{ width: '100%', height: 'auto', display: 'block' }}
                priority
              />
              <div className="roster-banner-overlay" />
              <div className="roster-banner-caption">
                <div className="roster-banner-title">8 Legend Roster · All Unlocked</div>
                <div className="roster-banner-tag">Instant Pick & Sprint</div>
              </div>
            </div>
          </div>

          {/* Interactive Grid & Spotlight */}
          <div className="roster-interactive-wrap">
            {/* Character Selector Grid */}
            <div className="roster-selector">
              {CHARACTERS.map((char, index) => {
                const isSelected = selectedCharacterIndex === index;
                return (
                  <button
                    key={char.id}
                    className={`roster-card ${isSelected ? 'active' : ''}`}
                    onClick={() => setSelectedCharacterIndex(index)}
                    type="button"
                  >
                    <Image
                      src={char.avatar}
                      alt={char.name}
                      width={54}
                      height={54}
                      className="roster-card-avatar"
                    />
                    <div className="roster-card-info">
                      <div className="roster-card-name">{char.name}</div>
                      <div className="roster-card-title">{char.title}</div>
                      <span className="roster-card-badge">{char.specialty}</span>
                    </div>
                  </button>
                );
              })}
            </div>

            {/* Hero Spotlight Card */}
            <div className="hero-spotlight">
              <div className="spotlight-top">
                <div className="spotlight-avatar-wrap">
                  <div className="spotlight-avatar-glow" />
                  <Image
                    src={activeHero.avatar}
                    alt={activeHero.name}
                    width={104}
                    height={104}
                    className="spotlight-avatar"
                    priority
                  />
                </div>
                <div className="spotlight-header-meta">
                  <span className="spotlight-archetype">{activeHero.archetype}</span>
                  <h3 className="spotlight-name">{activeHero.name}</h3>
                  <p className="spotlight-title">{activeHero.title}</p>
                </div>
              </div>

              <div className="spotlight-lore">
                &ldquo;{activeHero.description}&rdquo;
              </div>

              <div className="spotlight-stats">
                {/* Speed Rating */}
                <div className="stat-row">
                  <div className="stat-meta">
                    <span className="stat-label">⚡ Sprint Speed</span>
                    <span className="stat-val">{activeHero.speed.toFixed(2)}x ({(activeHero.speed * 100).toFixed(0)}%)</span>
                  </div>
                  <div className="stat-bar-track">
                    <div
                      className="stat-bar-fill speed"
                      style={{ width: `${Math.min(100, (activeHero.speed / 1.5) * 100)}%` }}
                    />
                  </div>
                </div>

                {/* Shield Rating */}
                <div className="stat-row">
                  <div className="stat-meta">
                    <span className="stat-label">🛡️ Shield Durability</span>
                    <span className="stat-val">{activeHero.shield.toFixed(2)}x ({(activeHero.shield * 100).toFixed(0)}%)</span>
                  </div>
                  <div className="stat-bar-track">
                    <div
                      className="stat-bar-fill shield"
                      style={{ width: `${Math.min(100, (activeHero.shield / 1.5) * 100)}%` }}
                    />
                  </div>
                </div>

                {/* Agility Rating */}
                <div className="stat-row">
                  <div className="stat-meta">
                    <span className="stat-label">🌪️ Obstacle Agility</span>
                    <span className="stat-val">{activeHero.agility.toFixed(2)}x ({(activeHero.agility * 100).toFixed(0)}%)</span>
                  </div>
                  <div className="stat-bar-track">
                    <div
                      className="stat-bar-fill agility"
                      style={{ width: `${Math.min(100, (activeHero.agility / 1.5) * 100)}%` }}
                    />
                  </div>
                </div>
              </div>

              <div className="spotlight-footer">
                <div className="spotlight-status">
                  <span>💛</span> 100% Unlocked & Playable
                </div>
                <Link
                  href="/download"
                  className="spotlight-cta"
                >
                  📥 Download APK & Play as {activeHero.name}
                </Link>
              </div>
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
            <p className="section-label">Real In-Game 3D Gameplay</p>
            <h2 className="section-title">Captured Live in Unity 6</h2>
          </div>

          <div className="screenshots-track">
            {SCREENSHOTS.map((s) => (
              <div key={s.src} className="screenshot-item">
                <div className="screenshot-img-wrap">
                  <span className="screenshot-badge">{s.badge}</span>
                  <Image
                    src={s.src}
                    alt={s.title}
                    width={640}
                    height={360}
                    style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                  />
                </div>
                <div className="screenshot-info">
                  <h3 className="screenshot-title">{s.title}</h3>
                  <p className="screenshot-caption">{s.caption}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── CHARACTER COMPARISON ── */}
      <RevealSection className="compare-section" id="compare">
        <div className="container">
          <p className="section-label">Head to Head</p>
          <h2 className="section-title">Compare Heroes</h2>
          <p className="roster-sub">
            Pick any two runners and see how their Speed, Shield, and Agility stack up against each other.
          </p>
          <CharacterCompare characters={CHARACTERS} />
        </div>
      </RevealSection>

      {/* ── HOW TO PLAY ── */}
      <RevealSection className="howto-section" id="how-to-play">
        <div className="container">
          <p className="section-label">Controls</p>
          <h2 className="section-title">How to Play</h2>
          <p className="roster-sub">
            Master five intuitive swipe gestures to navigate ancient ruins at full sprint.
          </p>
          <div className="howto-grid">
            {CONTROLS.map((c, i) => (
              <div key={i} className="howto-card">
                <div className="howto-gesture">{c.gesture}</div>
                <div className="howto-label">{c.label}</div>
                <div className="howto-action">{c.action}</div>
                <p className="howto-desc">{c.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </RevealSection>

      {/* ── FAQ ── */}
      <RevealSection className="faq-section" id="faq">
        <div className="container">
          <p className="section-label">Support</p>
          <h2 className="section-title">Frequently Asked</h2>
          <div className="faq-list">
            {FAQ.map((item, i) => (
              <FAQItem
                key={i}
                item={item}
                index={i}
                isOpen={faqOpenIndex === i}
                onToggle={() => setFaqOpenIndex(faqOpenIndex === i ? null : i)}
              />
            ))}
          </div>
        </div>
      </RevealSection>

      {/* ── DOWNLOAD ── */}
      <section className="download" id="download">
        <div className="download-glow" />
        <div className="container download-inner">
          <div className="download-badge">Native Android APK v1.7 · Character Abilities · Dynamic Music · 60 FPS</div>
          <h2 className="download-title">Ready for maximum performance?</h2>
          <p className="download-sub">
            Download the native APK v1.7 directly to your Android device with 7 unique character abilities, running trail effects, biome skybox transitions, dynamic music crossfade, and locked 60 FPS gameplay with all 8 heroes unlocked.
          </p>

          <div className="download-buttons">
            <Link className="download-apk" href="/download">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 16l-5-5h3V4h4v7h3l-5 5zm-7 4v-2h14v2H5z"/>
              </svg>
              Download APK v1.7 — Free (~114 MB)
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
            <div className="meta-chip"><span>✓</span> 8 Playable Heroes Unlocked</div>
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
            <li><a href="#roster">Roster</a></li>
            <li><a href="#compare">Compare</a></li>
            <li><a href="#how-to-play">How to Play</a></li>
            <li><a href="#features">Features</a></li>
            <li><a href="#screenshots">Screenshots</a></li>
            <li><a href="#faq">FAQ</a></li>
            <li><a href="#download">Download</a></li>
            <li><a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">GitHub</a></li>
          </ul>

          <div className="footer-divider" />

          <p className="footer-copy">
            © 2026 Spider Temple Escape. Built with Unity 6 & Next.js. &nbsp;
            <a href={GITHUB_URL} target="_blank" rel="noopener noreferrer">Open Source on GitHub</a>
          </p>
        </div>
      </footer>
    </>
  );
}
