import './globals.css';

export const metadata = {
  title: 'Spider Temple Escape — 3D Endless Runner',
  description:
    'Run, jump, slide and survive the ancient cursed temple in this adrenaline-pumping 3D endless runner for Android. Free download.',
  keywords: ['spider temple escape', 'android game', 'endless runner', '3d runner', 'mobile game'],
  openGraph: {
    title: 'Spider Temple Escape',
    description: 'Outrun the beast. Conquer the temple. Available free on Android.',
    url: 'https://akhilboss118-an.github.io/spider-temple-escape',
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
