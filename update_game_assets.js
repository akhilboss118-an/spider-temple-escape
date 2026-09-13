const fs = require('fs');
const path = require('path');
const https = require('https');

const assets = [
  {
    filename: 'jungle_escape_bg.png',
    url: 'https://lh3.googleusercontent.com/aida/AEtjO1VALQZtnuJCkQC8ukpSyHqUfoX5dkLKmweuyRseG81BlJSbAQYCSrohiI_LlI6C-uRSH24M060UOqdIYOPddcE8TnozY6kIPE4S_FFI13DesVtOM2RowytUXVqJKNJ5WC_A4EddKsVWIWQSsFC_Qonqr6d2AsIV5gJR7stlafwrS0FD5jE585s7QT3_QIgqxarcccLyLREiNw99U-34GTiNzuIAG4w0ZmxfTNGyjP8JtjvSsRd3MUAqfqo'
  },
  {
    filename: 'death_screen_bg.png',
    url: 'https://lh3.googleusercontent.com/aida/AEtjO1UrW948PUWRMlH1Mr1dzgSAZnP6jrRVky_lEe2txbKqxlKvVKCntRD5X5jcc3_EV_zriFywhxIlfR6IHiNIzl_hqJWOyWTCLHx3oGuV4FRMsFkr7baZhdtt_jWrinjJ38xt2fwgXbwkGxFHRdE7wq-A2W1MZhaT3DUWclvC4q4YQaMXUzxxIjMrjpv3QWf3dw9fjBaPCNPXy5YwKrW3fYuUL0hiEixl6ssUmkV_r7wBW-GEZE2bs2z60SVF'
  },
  {
    filename: 'golden_spider_relic.png',
    url: 'https://lh3.googleusercontent.com/aida/AEtjO1X61PiXSUdt_ScHuPJLRh77T0qGw8QGnni4r0R5s5dXX1w0Qs1BHFrMIxrciOJoTEqKr81za9VMtLsadl4XWTVcVS94ZfRZEod5J5bczr7Di-gpE3WtWe5FLms2ghFXfIW78ajt3mB9KkLRIMCgHR-0fG4N6rFbR6b5nQA3dX8UfwoJxrSIlK5_TLpZCKWHXRoh2uUBSH4v2gql25w4BKDxSW_uA4vYrKAgq5qL1o-sZ1CNMZBbk25prnfP'
  },
  {
    filename: 'hero_suit_preview.png',
    url: 'https://lh3.googleusercontent.com/aida/AEtjO1VIKsfgWYMCSeF0hNJZLZa5FVe5eHzTgjMj_X7Je1xe0YQozqIomZaZB8_h0wC-cIbWMjPcbIhBwQTSHR_RgpjV09YMFljydTaAjAoYuBeiEYhw2RQpZiYSZITEMbOayI6HnjAwP6h3T8DjNHPFIm_7JfzlyZ60oBz93xe9W1LYDVTGANxW3wM1kl5BvN5r9ZkBCW9sqEoPRTEnsdLyuFBAUkN0rGj-wkvXCna1OOslCs2VGsuslmZ_xdi7'
  }
];

function downloadFile(url, dest) {
  return new Promise((resolve, reject) => {
    const file = fs.createWriteStream(dest);
    https.get(url, (response) => {
      if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
        return downloadFile(response.headers.location, dest).then(resolve).catch(reject);
      }
      response.pipe(file);
      file.on('finish', () => {
        file.close(resolve);
      });
    }).on('error', (err) => {
      fs.unlink(dest, () => {});
      reject(err);
    });
  });
}

const targetDirs = [
  path.join(__dirname, 'Assets', 'Textures', 'UI'),
  path.join(__dirname, 'Assets', 'Resources', 'Textures')
];

async function main() {
  for (const dir of targetDirs) {
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  }

  for (const a of assets) {
    console.log(`Downloading ${a.filename}...`);
    for (const dir of targetDirs) {
      const dest = path.join(dir, a.filename);
      await downloadFile(a.url, dest);
      console.log(`  -> Saved to ${dest} (${fs.statSync(dest).size} bytes)`);
    }
  }
  console.log('All Unity assets successfully updated!');
}

main().catch(console.error);
