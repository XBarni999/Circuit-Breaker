"""Lay out actual Unity renders as readable GitHub weapon photographs."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
source = root / 'Validation~' / 'Gallery'
destination = root / 'Validation~' / 'GalleryDocs'
destination.mkdir(parents=True, exist_ok=True)
background = (14, 19, 24)
title_font = ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf', 48)
small_font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 23)
label_font = ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf', 20)
items = [
    ('blackout', 'AGM-180 BLACKOUT', 'High-power microwave cruise missile', 'DEPLOYED FLIGHT CONFIGURATION'),
    ('locust', 'CBU-82M LOCUST', 'Area-denial mine dispenser', 'CLOSED DISPENSER'),
    ('locust-mine', 'LOCUST SUBMUNITION', '7 kg HE mine / eight per dispenser', 'DEPLOYED FINS'),
    ('blackout-rack', 'BLACKOUT / TRIPLE RACK', 'Three missiles on one heavy weapon station', 'STOWED CONFIGURATION'),
    ('locust-rack', 'LOCUST / TRIPLE RACK', 'Three dispensers on one heavy weapon station', 'CURRENT IN-GAME MODELS'),
]
for name, title, subtitle, label in items:
    render = Image.open(source / f'{name}.png').convert('RGB')
    canvas = Image.new('RGB', (1600, 1120), background)
    canvas.paste(render, (0, 140))
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((55, 48, 60, 111), fill=(148, 179, 194))
    draw.text((82, 38), title, font=title_font, fill=(232, 237, 239))
    draw.text((84, 97), subtitle, font=small_font, fill=(159, 175, 186))
    draw.line((60, 1041, 1540, 1041), fill=(52, 67, 78), width=1)
    draw.text((60, 1065), 'CIRCUIT BREAKER  /  NUCLEAR OPTION', font=label_font, fill=(159, 175, 186))
    draw.text((1540, 1065), label, font=label_font, fill=(159, 175, 186), anchor='ra')
    canvas.save(destination / f'{name}.png', optimize=True)
    print(destination / f'{name}.png')
