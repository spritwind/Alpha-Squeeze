#!/usr/bin/env python3
"""
gRPC 程式碼生成腳本

從 squeeze.proto 生成 Python gRPC 程式碼。

使用方式：
    cd python
    python scripts/generate_grpc.py

或直接執行：
    python -m scripts.generate_grpc
"""

import subprocess
import sys
from pathlib import Path


def main():
    """生成 gRPC Python 程式碼"""
    # 取得專案根目錄
    script_dir = Path(__file__).parent
    python_dir = script_dir.parent
    proto_dir = python_dir.parent / "proto"
    output_dir = python_dir / "engine" / "protos"

    # 確保輸出目錄存在
    output_dir.mkdir(parents=True, exist_ok=True)

    # Proto 檔案路徑
    proto_file = proto_dir / "squeeze.proto"

    if not proto_file.exists():
        print(f"錯誤：找不到 proto 檔案: {proto_file}")
        sys.exit(1)

    print(f"Proto 檔案: {proto_file}")
    print(f"輸出目錄: {output_dir}")

    # 執行 grpc_tools.protoc
    cmd = [
        sys.executable, "-m", "grpc_tools.protoc",
        f"-I{proto_dir}",
        f"--python_out={output_dir}",
        f"--grpc_python_out={output_dir}",
        str(proto_file),
    ]

    print(f"執行命令: {' '.join(cmd)}")

    try:
        result = subprocess.run(cmd, check=True, capture_output=True, text=True)
        print("gRPC 程式碼生成成功！")

        # 列出生成的檔案
        generated_files = list(output_dir.glob("squeeze_pb2*.py"))
        for f in generated_files:
            print(f"  生成: {f.name}")

        # 修正 import 路徑（protoc 生成的 import 需要調整）
        fix_imports(output_dir)

    except subprocess.CalledProcessError as e:
        print(f"錯誤：gRPC 程式碼生成失敗")
        print(f"stdout: {e.stdout}")
        print(f"stderr: {e.stderr}")
        sys.exit(1)
    except FileNotFoundError:
        print("錯誤：找不到 grpc_tools 模組")
        print("請先安裝：pip install grpcio-tools")
        sys.exit(1)


def fix_imports(output_dir: Path):
    """修正生成程式碼中的 import 路徑"""
    grpc_file = output_dir / "squeeze_pb2_grpc.py"

    if grpc_file.exists():
        content = grpc_file.read_text()
        # 修正相對 import
        fixed_content = content.replace(
            "import squeeze_pb2",
            "from . import squeeze_pb2"
        )
        grpc_file.write_text(fixed_content)
        print("  已修正 import 路徑")


if __name__ == "__main__":
    main()
