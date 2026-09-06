import './globals.css';

export const metadata = {
  title: 'Spider Temple Escape — Play as Spider-Man vs Zombie Monster',
  description:
    'Play as Spider-Man and escape the ancient cursed temple while being hunted by a terrifying Zombie monster in this 3D endless runner for Android. Free download.',
  keywords: ['spider temple escape', 'spiderman runner', 'zombie monster', 'android game', 'endless runner', '3d runner', 'mobile game'],
  openGraph: {
    title: 'Spider Temple Escape — Spider-Man vs Zombie Monster',
    description: 'Play as Spider-Man! Outrun the terrifying Zombie monster in an ancient temple. Available free on Android.',
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
