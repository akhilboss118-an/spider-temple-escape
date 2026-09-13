const sharp = require('sharp');
const path = require('path');

async function createFaceGrid() {
  const imagesDir = 'C:\\Users\\akhil\\OneDrive\\Desktop\\images';
  const outDir = 'C:\\Users\\akhil\\.gemini\\antigravity-ide\\brain\\6654025e-83b4-4832-8826-1822ce8e6f5e';

  // Specific face crops for each character
  const chars = [
    // 1. Akhilboss: width 1792, height 2400 -> head is near top center
    { name: 'Akhilboss', file: 'Akhilboss.png', crop: { left: 420, top: 40, width: 950, height: 950 } },
    // 2. Harika: width 2048, height 2048 -> head is top center
    { name: 'Harika', file: 'Harika.png', crop: { left: 580, top: 40, width: 950, height: 950 } },
    // 3. Nandini: width 890, height 1024 -> head is top center
    { name: 'Nandini', file: 'Nandini .webp', crop: { left: 240, top: 0, width: 440, height: 440 } },
    // 4. Navaneeth: width 1536, height 2752 -> head is around top 10% - 30%
    { name: 'Navaneeth', file: 'Navaneeth.png', crop: { left: 460, top: 380, width: 620, height: 620 } },
    // 5. Pavan: width 1536, height 2752
    { name: 'Pavan', file: 'pavan.png', crop: { left: 450, top: 500, width: 640, height: 640 } },
    // 6. Pravalika: width 2048, height 2048
    { name: 'Pravalika', file: 'Pravalika.png', crop: { left: 600, top: 80, width: 920, height: 920 } },
    // 7. Srikar: width 1536, height 2752
    { name: 'Srikar', file: 'Srikar (1).png', crop: { left: 470, top: 280, width: 600, height: 600 } }
  ];

  const W = 450;
  const H = 450;
  const processed = [];

  for (const c of chars) {
    const filePath = path.join(imagesDir, c.file);
    const buf = await sharp(filePath)
      .extract(c.crop)
      .resize(W, H, { fit: 'cover' })
      .toBuffer();

    processed.push({ name: c.name, buf });
  }

  // 4 columns, 2 rows
  const canvasW = W * 4;
  const canvasH = H * 2;
  const composites = [];

  processed.forEach((p, idx) => {
    const col = idx % 4;
    const row = Math.floor(idx / 4);
    composites.push({
      input: p.buf,
      top: row * H,
      left: col * W
    });
  });

  const outputGridPath = path.join(outDir, 'characters_faces_reference.jpg');
  await sharp({
    create: {
      width: canvasW,
      height: canvasH,
      channels: 3,
      background: { r: 15, g: 15, b: 20 }
    }
  })
  .composite(composites)
  .jpeg({ quality: 95 })
  .toFile(outputGridPath);

  console.log('Saved enhanced character faces reference to:', outputGridPath);
}

createFaceGrid().catch(console.error);
