"""Serve the real browser client and shared images for local UI verification."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import argparse

REPO = Path(__file__).resolve().parents[2]


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(REPO / 'companion/SimDeck.App/Browser'), **kwargs)

    def translate_path(self, path):
        if path.split('?', 1)[0] in ('/fs25-field-ui.json', '/beamng-damage-ui.json', '/fs25-farm-ui.json'):
            return str(REPO / 'assets' / path.split('?', 1)[0].lstrip('/'))
        if path.split('?', 1)[0] == '/f1-damage-zones.json':
            return str(REPO / 'assets/f1-damage-zones.json')
        if path.startswith('/vehicles/'):
            name = path.split('?', 1)[0].removeprefix('/vehicles/')
            if name in {'farm.png', 'road.png', 'equipment.png', 'gt.png', 'harvest.png', 'fs25-flat-1.png', 'fs25-flat-2.png', 'fs25-flat-3.png', 'fs25-flat-4.png', 'fs25-flat-5.png', 'fs25-flat-6.png'}:
                return str(REPO / 'assets/vehicles' / name)
            return str(REPO / 'assets/vehicles/__not_found__')
        return super().translate_path(path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=8765)
    args = parser.parse_args()
    ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
