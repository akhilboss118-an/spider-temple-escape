const fs = require('fs');
const path = require('path');
const https = require('https');

const screens = [
  {
    name: 'loading_screen.html',
    url: 'https://contribution.usercontent.google.com/download?c=CgthaWRhX2NvZGVmeBJ8Eh1hcHBfY29tcGFuaW9uX2dlbmVyYXRlZF9maWxlcxpbCiVodG1sXzRkOTk3NDM4NTkxNjQzNjk4ZjIxMGZkM2NkMGQ2ZDIyEgsSBxDt4t-mngsYAZIBJAoKcHJvamVjdF9pZBIWQhQxMjMyNDQzOTA4NDc5NzQ2MjY3OA&filename=&opi=96797242'
  },
  {
    name: 'mainmenu_screen.html',
    url: 'https://contribution.usercontent.google.com/download?c=CgthaWRhX2NvZGVmeBJ8Eh1hcHBfY29tcGFuaW9uX2dlbmVyYXRlZF9maWxlcxpbCiVodG1sXzlmOTljYTExYjViMjQwMmY4MTVjOGFkMzQ2OGI3M2RlEgsSBxDt4t-mngsYAZIBJAoKcHJvamVjdF9pZBIWQhQxMjMyNDQzOTA4NDc5NzQ2MjY3OA&filename=&opi=96797242'
  },
  {
    name: 'death_screen.html',
    url: 'https://contribution.usercontent.google.com/download?c=CgthaWRhX2NvZGVmeBJ8Eh1hcHBfY29tcGFuaW9uX2dlbmVyYXRlZF9maWxlcxpbCiVodG1sX2EwNzAxYWNmYTRjZTRlODA4YzQ2YWFjMjkwZTU3MWY5EgsSBxDt4t-mngsYAZIBJAoKcHJvamVjdF9pZBIWQhQxMjMyNDQzOTA4NDc5NzQ2MjY3OA&filename=&opi=96797242'
  },
  {
    name: 'loading_screen.png',
    url: 'https://lh3.googleusercontent.com/aida/AEtjO1XCT07-wMJ9gAkquFowL0BVKcyWMASy0b5wqB6DCiK_ORr9KzIUWosQid7EsGa8fxTHPQPYBMfTCY99MbGPHbl7yTCW-taXHEy6rCCwnTl4wKa_xGU5uu2UYz-c5KIRD-HCKAsXbNSDVbN8oGFjnm65hpQd_qJuSH80H2SYYTlIqesEuyKevSLFeu2tzIvkmBpMVw6O2izYBzmKLk2YxZ2iqPEFwjo1CjEzeAZNhdcv_GT_tqz5O8tEfWc'
  },
  {
    name: 'mainmenu_screen.png',
    url: 'https://lh3.googleusercontent.com/aida/AEtjO1Vv-Qxivn4lrL-Yc9izsRlNRdsKAuH6h7lFwsOGygSiLKQDLthn6iNX0b-8WO7c5dpfTz0ICqEo2Dpaz1SYxc2-2M6xPiqIe5VU4TNo6xLUXRTzfs0a40Y9z6EzmCuyVMBdcrnX-CCKFnNaGPV7J6-qZ_OrF_5u7-0R9ygG3ZXisvnafYO_sNWP8QyQmkrrlqzyrFkjVPmQ6vuKkxu8fOFzPHfgPPSu8dA0LIWf085Ajk_xaHfuHzqEAfo'
  },
  {
    name: 'death_screen.png',
    url: 'https://lh3.googleusercontent.com/aida/AEtjO1VO1XK3sr9gn8_ug_kRoIyxcL8nNalVDsCqDnKWWU6phBJn5AA1C7wFLudqQaXLus3GCasvyEbPshRSUlQ35bxuVIyDC7eMGOIlVBind8kfYYnyYREg1zeFRAgpmXLFhsYpE4y4GMAhDmsNsyMWeHgM3ocmmcEjZTM_GRtvtrXZBefYMUFcPrum6Qc7dYADD-TRFvmr-FDw_I-aQnrz0YS-Ijf3kI-eEzR6_LgxU48UmoR9okXDeloakJvv'
  }
];

const targetDir = path.join(__dirname, 'web', 'public', 'stitch');
if (!fs.existsSync(targetDir)) {
  fs.mkdirSync(targetDir, { recursive: true });
}

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

async function main() {
  for (const s of screens) {
    const dest = path.join(targetDir, s.name);
    console.log(`Downloading ${s.name}...`);
    await downloadFile(s.url, dest);
    console.log(`Saved ${s.name} (${fs.statSync(dest).size} bytes)`);
  }
  console.log('All downloads complete!');
}

main().catch(console.error);
