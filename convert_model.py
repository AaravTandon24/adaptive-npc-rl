"""Convert an ONNX model to Opset 15 for Unity Sentis compatibility.

ML-Agents stores weights in external .onnx.data files alongside checkpoints.
The final EnemyAgent.onnx at the run root references those data files but they
live in the EnemyAgent/ subdirectory.  This script handles that by loading
without external data, resolving the data from the checkpoint directory, and
saving a self-contained model (no external data) for Unity.
"""
import sys
import os

try:
    import onnx
    from onnx import version_converter
    from onnx.external_data_helper import load_external_data_for_model
except ImportError:
    print("Error: The 'onnx' package is not installed in this Python environment.")
    print("Please run: pip install onnx")
    sys.exit(1)


def find_model_with_data(run_dir):
    """Find the latest checkpoint .onnx that has its .data file next to it."""
    agent_dir = None
    # Look for the agent subdirectory (e.g., results/run_id/EnemyAgent/)
    for entry in os.listdir(run_dir):
        candidate = os.path.join(run_dir, entry)
        if os.path.isdir(candidate) and entry != "run_logs":
            agent_dir = candidate
            break

    if agent_dir is None:
        return None

    # Find all .onnx files that have matching .data files
    onnx_files = []
    for f in os.listdir(agent_dir):
        if f.endswith(".onnx") and not f.endswith(".onnx.data"):
            data_file = f + ".data"
            # The data file name is based on the onnx filename stem
            stem = f.replace(".onnx", "")
            data_candidate = stem + ".onnx.data"
            if os.path.exists(os.path.join(agent_dir, data_candidate)):
                onnx_files.append((os.path.join(agent_dir, f), stem))

    if not onnx_files:
        return None

    onnx_files.sort(key=lambda item: os.path.getmtime(item[0]), reverse=True)
    return onnx_files[0][0]


def convert_model(input_path, output_path):
    """Load an ONNX model and convert it to Opset 15."""
    # If input_path is a run directory (has subdirectories), find the best checkpoint
    if os.path.isdir(input_path):
        resolved = find_model_with_data(input_path)
        if resolved:
            print(f"Resolved to checkpoint: {resolved}")
            input_path = resolved
        else:
            print(f"Error: Could not find a valid .onnx model with data in {input_path}")
            sys.exit(1)

    if not os.path.exists(input_path):
        print(f"Error: Input model not found: {input_path}")
        sys.exit(1)

    base_dir = os.path.dirname(input_path)
    print(f"Loading model from: {input_path}")
    print(f"External data directory: {base_dir}")

    # Load without external data first, then resolve from the correct directory
    model = onnx.load(input_path, load_external_data=False)
    load_external_data_for_model(model, base_dir)

    current_opset = model.opset_import[0].version
    print(f"Current opset version: {current_opset}")

    if current_opset == 15:
        print("Model is already at opset 15.")
    else:
        print(f"Converting from opset {current_opset} to opset 15...")
        model = version_converter.convert_version(model, 15)
        print("Conversion successful.")

    # Ensure output directory exists
    output_dir = os.path.dirname(output_path)
    if output_dir:
        os.makedirs(output_dir, exist_ok=True)

    # Save as a self-contained model (all weights embedded, no external data)
    onnx.save(model, output_path)
    print(f"Saved converted model to: {output_path}")
    print(f"Model size: {os.path.getsize(output_path)} bytes")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python convert_model.py <input.onnx | run_directory> <output.onnx>")
        sys.exit(1)
    convert_model(sys.argv[1], sys.argv[2])
