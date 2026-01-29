#!/usr/bin/env python
import cbor2, argparse, hashlib, json

def cert_chain_sha256(file_path):
    with open(file_path, 'rb') as fp:
        cose_obj = cbor2.load(fp)
        if cose_obj.tag != 18:  # COSE_Sign1_TAG
            raise ValueError(f"Expected tag 18, got {cose_obj.tag}")
        cert_chain = cose_obj.value[1].get(33)  # COSE_X5CHAIN
        if not cert_chain:
            raise ValueError("x5chain not found in unprotected header")
        return json.dumps([hashlib.sha256(cert).hexdigest() for cert in cert_chain])

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('-f', '--file-path', type=str, required=True, help='Path to COSE signature envelope')
    args = parser.parse_args()
    print(cert_chain_sha256(args.file_path))
