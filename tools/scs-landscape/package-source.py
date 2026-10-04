"""Bundle corresponding GPL extractor sources with the Windows distribution."""
from pathlib import Path
import argparse, json, shutil, urllib.request, xml.etree.ElementTree as ET, zipfile, io

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--packages', default=str(Path.home()/'.nuget/packages'))
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
out = Path(args.output)
for directory in ['tools/scs-landscape', 'third_party/ts-map']:
    for file in (repo/directory).rglob('*'):
        if file.is_file() and not {'bin', 'obj', '__pycache__'}.intersection(file.relative_to(repo/directory).parts):
            target = out/file.relative_to(repo)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(file, target)
shutil.copy2(repo/'Directory.Build.props', out/'Directory.Build.props')
manifest=[]
for package,version in [('TruckLib','0.5.1'),('TruckLib.Core','0.3.0'),('TruckLib.HashFs','0.2.6'),('TruckLib.Models','0.4.2'),('TruckLib.Sii','0.2.1')]:
    folder=Path(args.packages)/package.lower()/version
    tree=ET.parse(next(folder.glob('*.nuspec')))
    meta=next(e for e in tree.iter() if e.tag.endswith('}metadata'))
    source=next(e for e in meta if e.tag.endswith('}repository'))
    url=source.attrib['url'].replace('https://github.com/', 'https://api.github.com/repos/')+'/zipball/'+source.attrib['commit']
    req=urllib.request.Request(url,headers={'User-Agent':'SimDeck-source-packager'})
    with urllib.request.urlopen(req,timeout=90) as response:
        data=response.read(30_000_001)
    if len(data)>30_000_000 or not zipfile.is_zipfile(io.BytesIO(data)):raise RuntimeError('Invalid dependency source archive')
    target=out/'vendor'/f'{package}-{version}.zip';target.parent.mkdir(parents=True,exist_ok=True)
    # The upstream repositories contain game models in test fixtures and images
    # in docs. Package library compilation sources only, never those assets.
    with zipfile.ZipFile(io.BytesIO(data)) as original, zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as bundled:
        for item in original.infolist():
            parts=Path(item.filename).parts
            if item.is_dir():continue
            if len(parts)>2 and parts[1]!=package:continue
            if len(parts)==2 and Path(parts[-1]).suffix.lower() not in ('.md','.txt','.props','.targets','.json','.config') and parts[-1]!='LICENSE':continue
            bundled.writestr(item.filename,original.read(item))
    manifest.append(dict(package=package,version=version,repository=source.attrib['url'],commit=source.attrib['commit']))
(out/'dependencies.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
(out/'README.txt').write_text('Build tools/scs-landscape/SimDeck.ScsLandscape.csproj with .NET 10.\nVendor archives contain the exact library compilation sources and licenses.\nUpstream test fixtures and documentation screenshots are intentionally excluded: they are not needed to build the shipped libraries and may contain game assets.\n',encoding='utf-8')
print('Extractor source and five corresponding dependency source archives bundled.')
