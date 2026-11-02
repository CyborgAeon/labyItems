#!/usr/bin/env python3
"""Convert PDFKit character coordinates to a monthly SQLite seed, without text-order guesses."""
import argparse
import json
import re
from pathlib import Path


def extract(pages):
    result = []
    for page in pages:
        rows = {}
        for char in page['chars']:
            if char['t'] not in '\r\n':
                rows.setdefault(round(char['y'], 1), []).append(char)
        def text(chars):
            return ''.join(c['t'] for c in sorted(chars, key=lambda c: c['x'])).strip()
        headers = sorted((c for c in rows[781.4] if c['t'].strip()), key=lambda c: c['x'])
        # Each numeric column is centered beneath its own region heading.
        groups = [[]]
        for c in headers:
            if groups[-1] and c['x'] - (groups[-1][-1]['x'] + groups[-1][-1]['w']) > 15:
                groups.append([])
            groups[-1].append(c)
        centers = [(g[0]['x'] + g[-1]['x'] + g[-1]['w']) / 2 for g in groups]
        # Preserve spaces from the source headings, including multiword names.
        bounds = [340] + [(a+b)/2 for a,b in zip(centers, centers[1:])] + [600]
        names = [text([c for c in rows[781.4] if low<=c['x']<high]) for low,high in zip(bounds,bounds[1:])]
        for y, chars in sorted(rows.items(), reverse=True):
            if y > 770:
                continue
            label = text([c for c in chars if c['x'] < 150])
            if not label:
                continue
            category = 'Administration' if y > 650 else 'Dragons' if y > 440 else 'Deities and Orders'
            if label == 'Defense (Previously Stability)':
                label = 'Defense'
            for name, low, high in zip(names, bounds, bounds[1:]):
                value = text([c for c in chars if low <= c['x'] < high])
                if not value:
                    continue  # An absent source value is not zero.
                if not re.fullmatch(r'-?\d+(?:\.\d+)?', value):
                    raise ValueError(f'Unexpected value on page {page["page"]}, {name}, {label}: {value!r}')
                result.append(dict(region=name, category=category, name=label, value=int(value), page=page['page']))
        for name in names:
            records=[r for r in result if r['region']==name]
            assert sum(r['category']=='Administration' for r in records)==8, name
            assert sum(r['category']=='Dragons' for r in records)==14, name
    return result


def sql(rows, year, month):
    def quote(value): return "'" + str(value).replace("'", "''") + "'"
    lines = ['''CREATE TABLE IF NOT EXISTS WorldStatistic (
    Year INTEGER NOT NULL CHECK(Year BETWEEN 1 AND 9999),
    Month INTEGER NOT NULL CHECK(Month BETWEEN 1 AND 12),
    RegionId TEXT NOT NULL, Category TEXT NOT NULL, Name TEXT NOT NULL,
    Value NUMERIC NOT NULL, Ordinal INTEGER NOT NULL, SourcePage INTEGER NOT NULL,
    PRIMARY KEY(Year, Month, RegionId, Category, Name)
);''']
    lines.append(f'DELETE FROM WorldStatistic WHERE Year={year} AND Month={month};')
    for i,r in enumerate(rows):
        lines.append('INSERT OR REPLACE INTO WorldStatistic VALUES (' + ','.join(map(str,[year,month])) + ',' + ','.join(quote(r[k]) for k in ['region','category','name']) + f',{r["value"]},{i},{r["page"]});')
    return '\n'.join(lines)+'\n'

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('layout');parser.add_argument('--year',type=int,required=True);parser.add_argument('--month',type=int,required=True);parser.add_argument('--output',required=True)
    args=parser.parse_args()
    assert 1<=args.month<=12 and 1<=args.year<=9999
    data=extract(json.loads(Path(args.layout).read_text()))
    Path(args.output).write_text(sql(data,args.year,args.month))
    print(f'Imported {len(data)} values for {len(set(r["region"] for r in data))} places')
