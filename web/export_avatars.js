const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

async function exportCharacterAvatars() {
  const imagesDir = 'C:\\Users\\akhil\\OneDrive\\Desktop\\images';
  const outDir = 'C:\\Users\\akhil\\OneDrive\\Desktop\\New folder\\web\\public\\characters';

  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  const chars = [
    { id: 'akhilboss', file: 'Akhilboss.png', crop: { left: 420, top: 40, width: 950, height: 950 } },
    { id: 'harika', file: 'Harika.png', crop: { left: 580, top: 40, width: 950, height: 950 } },
    { id: 'nandini', file: 'Nandini .webp', crop: { left: 240, top: 0, width: 440, height: 440 } },
    { id: 'navaneeth', file: 'Navaneeth.png', crop: { left: 460, top: 380, width: 620, height: 620 } },
    { id: 'pavan', file: 'pavan.png', crop: { left: 450, top: 500, width: 640, height: 640 } },
    { id: 'pravalika', file: 'Pravalika.png', crop: { left: 600, top: 80, width: 920, height: 920 } },
    { id: 'srikar', file: 'Srikar (1).png', crop: { left: 470, top: 280, width: 600, height: 600 } }
  ];

  for (const c of chars) {
    const src = path.join(imagesDir, c.file);
    const dest = path.join(outDir, `${c.id}.png`);
    await sharp(src)
      .extract(c.crop)
      .resize(256, 256, { fit: 'cover' })
      .png({ quality: 90 })
      .toFile(dest);
    console.log(`Exported avatar: ${dest}`);
  }

  // Export spider-man avatar from app_logo or character_select_roster
  const rosterPath = 'C:\\Users\\akhil\\OneDrive\\Desktop\\New folder\\web\\public\\character_select_roster.jpg';
  const spidermanDest = path.join(outDir, 'spiderman.png');
  const rosterMeta = await sharp(rosterPath).metadata();
  // Spider-man on left: left: ~2%, top: ~40%, width: ~20%, height: ~50%
  const sW = Math.round(rosterMeta.width * 0.20);
  const sH = Math.round(rosterMeta.height * 0.40);
  const sL = Math.round(rosterMeta.width * 0.03);
  const sT = Math.round(rosterMeta.height * 0.42);

  await sharp(rosterPath)
    .extract({ left: sL, top: sT, width: sW, height: sH })
    .resize(256, 256, { fit: 'cover' })
    .png({ quality: 90 })
    .toFile(spidermanDest);

  console.log(`Exported spiderman avatar: ${spidermanDest}`);
}

exportCharacterAvatars().catch(console.error);
