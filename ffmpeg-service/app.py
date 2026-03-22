import os
import subprocess
from flask import Flask, request, jsonify

app = Flask(__name__)

@app.route('/health')
def health():
    return jsonify({'ok': True})

@app.route('/thumbnail', methods=['POST'])
def thumbnail():
    data = request.get_json()
    input_path  = data.get('input')
    output_path = data.get('output')
    timestamp   = data.get('timestamp', '00:00:01')

    if not input_path or not output_path:
        return jsonify({'ok': False, 'error': 'input and output required'}), 400

    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    try:
        result = subprocess.run(
            ['ffmpeg', '-i', input_path, '-ss', timestamp,
             '-frames:v', '1', '-q:v', '2', output_path, '-y'],
            capture_output=True, timeout=60
        )
        if result.returncode == 0 and os.path.exists(output_path):
            return jsonify({'ok': True})
        return jsonify({'ok': False, 'error': result.stderr.decode('utf-8', errors='replace')}), 500
    except Exception as e:
        return jsonify({'ok': False, 'error': str(e)}), 500

@app.route('/transcode', methods=['POST'])
def transcode():
    data = request.get_json()
    input_path  = data.get('input')
    output_path = data.get('output')

    if not input_path or not output_path:
        return jsonify({'ok': False, 'error': 'input and output required'}), 400

    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    try:
        result = subprocess.run(
            ['ffmpeg', '-i', input_path,
             '-c:v', 'libx264', '-preset', 'fast', '-crf', '23',
             '-c:a', 'aac', '-b:a', '128k',
             '-movflags', '+faststart',
             output_path, '-y'],
            capture_output=True, timeout=300
        )
        if result.returncode == 0 and os.path.exists(output_path):
            return jsonify({'ok': True})
        return jsonify({'ok': False, 'error': result.stderr.decode('utf-8', errors='replace')}), 500
    except Exception as e:
        return jsonify({'ok': False, 'error': str(e)}), 500

@app.route('/convert-image', methods=['POST'])
def convert_image():
    data = request.get_json()
    input_path  = data.get('input')
    output_path = data.get('output')

    if not input_path or not output_path:
        return jsonify({'ok': False, 'error': 'input and output required'}), 400

    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    try:
        result = subprocess.run(
            ['ffmpeg', '-i', input_path, '-vframes', '1', '-q:v', '2', output_path, '-y'],
            capture_output=True, timeout=60
        )
        if result.returncode == 0 and os.path.exists(output_path):
            return jsonify({'ok': True})
        return jsonify({'ok': False, 'error': result.stderr.decode('utf-8', errors='replace')}), 500
    except Exception as e:
        return jsonify({'ok': False, 'error': str(e)}), 500

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5100)
