import './globals.css';

export const metadata = {
  title: 'Spider Temple Escape — 8 Playable Heroes · Akhilboss Edition',
  description:
    'Play as Spider-Man, Akhilboss, and 6 additional custom heroes in this high-octane 3D endless runner. Outrun the ancient temple beast with unique speed, shield, and agility stats. Free Android APK.',
  keywords: ['spider temple escape', 'akhilboss', '8 playable heroes', 'spiderman runner', 'zombie monster', 'android game', 'endless runner', '3d runner', 'mobile game'],
  openGraph: {
    title: 'Spider Temple Escape — 8 Playable Heroes · Akhilboss Edition',
    description: 'Sprint through ancient cursed ruins with Spider-Man, Akhilboss, and 6 unique heroes! Outrun the relentless beast guardian on Android.',
    url: 'https://spider-temple-escape.vercel.app',
    siteName: 'Spider Temple Escape',
    type: 'website',
  },
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
