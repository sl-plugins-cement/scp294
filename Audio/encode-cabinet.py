"""Encode a source clip for the native 48 kHz mono, 10 ms SpeakerToy decoder."""
import argparse
from pathlib import Path
import struct
import subprocess
import tempfile


def packets(data):
    offset = 0
    pending = bytearray()
    while offset < len(data):
        if data[offset:offset + 4] != b'OggS':
            raise ValueError('Invalid Ogg page')
        count = data[offset + 26]
        sizes = data[offset + 27:offset + 27 + count]
        offset += 27 + count
        for size in sizes:
            pending.extend(data[offset:offset + size])
            offset += size
            if size < 255:
                yield bytes(pending)
                pending.clear()
    if pending:
        raise ValueError('Truncated Ogg packet')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory() as temp:
        encoded = Path(temp) / 'cabinet.opus'
        # Native SpeakerToy playback is 3 dB quieter; match AudioTransmitter's correction.
        subprocess.run(['ffmpeg', '-v', 'error', '-i', str(args.source), '-vn',
                        '-map_metadata', '-1', '-ac', '1', '-ar', '48000',
                        '-af', 'volume=1.41421356237', '-c:a', 'libopus',
                        '-application', 'audio', '-frame_duration', '10',
                        '-b:a', '16k', '-vbr', 'off', str(encoded)], check=True)
        all_packets = list(packets(encoded.read_bytes()))
    if not all_packets[0].startswith(b'OpusHead') or not all_packets[1].startswith(b'OpusTags'):
        raise ValueError('Missing Opus headers')
    audio = all_packets[2:]
    # Hard CBR at 16 kb/s with 10 ms frames must produce 20-byte packets.
    if not audio or any(len(packet) != 20 for packet in audio):
        raise ValueError('Expected 16 kb/s, 10 ms CBR Opus packets')
    args.output.write_bytes(b'R294OP01' + struct.pack('<i', len(audio)) +
                           b''.join(struct.pack('<H', len(packet)) + packet for packet in audio))
    print(f'{len(audio) / 100:.2f} seconds, {args.output.stat().st_size:,} bytes, 16 kb/s mono')


if __name__ == '__main__':
    main()
