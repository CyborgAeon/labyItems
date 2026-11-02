#!/usr/bin/env python3
"""Extract the original embedded PNG without re-encoding or changing its pixels."""
import argparse
import base64
import struct
import xml.etree.ElementTree as ET
from pathlib import Path

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('source');parser.add_argument('output')
    args=parser.parse_args()
    root=ET.parse(args.source).getroot()
    assert root.get('viewBox')=='0 0 2048 1536', 'Unexpected coordinate system'
    images=[e for e in root.iter() if e.tag.split('}')[-1]=='image']
    assert len(images)==1, 'Expected one original background image'
    image=images[0]
    assert image.get('x','0')=='0' and image.get('y','0')=='0' and not image.get('transform'), 'Unexpected image transform'
    href=image.get('href') or image.get('{http://www.w3.org/1999/xlink}href')
    assert href.startswith('data:image/png;base64,'), 'Expected an embedded offline PNG'
    data=base64.b64decode(href.split(',',1)[1],validate=True)
    assert data[:8]==b'\x89PNG\r\n\x1a\n', 'Invalid PNG header'
    assert struct.unpack('>II',data[16:24])==(2048,1536), 'Unexpected PNG dimensions'
    Path(args.output).write_bytes(data)
    print(f'Extracted {len(data)} original PNG bytes')
