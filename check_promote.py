#!/usr/bin/env python3
"""Check what --all-approved actually does by examining its argparse."""
import ast, sys

with open('D:/AEON/scripts/promote.py') as f:
    content = f.read()

# Parse the argparse setup
tree = ast.parse(content)

# Find the main function or argument parsing
for node in ast.walk(tree):
    if isinstance(node, ast.FunctionDef) and node.name == 'main':
        print(f"Function: main")
        for child in ast.walk(node):
            if isinstance(child, ast.arguments):
                for arg in child.args:
                    if isinstance(arg, ast.arg):
                        print(f"  Arg: {arg.arg}")
                # Print defaults
                if node.body and hasattr(node.body[0], 'decorator_list'):
                    pass
        # Look for --all-approved in the code text
        if '--all-approved' in content:
            print("\n--all-approved found in source")
        else:
            print("\n--all-approved NOT found in source")
    
# Just search the source for --all-approved patterns
import re
matches = re.findall(r'--all-approved[^\s]*', content)
print(f"\n--all-approved references in source: {matches}")
print(f"\nTotal length of source: {len(content)} chars")