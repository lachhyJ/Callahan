"""Writes CALLAHAN_TOKEN (an env var, never argv or stdin — see
bake-and-build.sh) into properties.xml's AuthToken property in place."""
import os
import re
import sys
from xml.sax.saxutils import escape

path = sys.argv[1]
token = os.environ["CALLAHAN_TOKEN"].strip()

with open(path) as f:
    content = f.read()

new_content, count = re.subn(
    r'(<property id="AuthToken" type="string">)[^<]*(</property>)',
    lambda m: m.group(1) + escape(token) + m.group(2),
    content,
)
if count != 1:
    print(f"Expected exactly one AuthToken property, found {count}.", file=sys.stderr)
    sys.exit(1)

with open(path, "w") as f:
    f.write(new_content)
