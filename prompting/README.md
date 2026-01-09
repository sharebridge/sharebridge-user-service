# Prompting Folder

This folder contains all prompts used to generate code, documentation, and configurations for this component.

## Purpose

- **Transparency:** Track how features are specified and implemented
- **Reproducibility:** Allow regeneration of code with similar/improved prompts
- **Learning:** Help contributors understand effective prompting techniques
- **Collaboration:** Enable non-technical contributors to participate

## Structure

Prompts should be organized by:
- Feature area
- Date of creation
- AI model used (if relevant)

## How to Add Prompts

When generating code or documentation:
1. Save the prompt text in this folder
2. Name it descriptively: YYYY-MM-DD-feature-name-prompt.md
3. Include context, constraints, and expected outputs
4. Reference related files or issues

## Template

\\\markdown
# Prompt: [Feature Name]

**Date:** YYYY-MM-DD  
**Purpose:** Brief description  
**AI Model:** GPT-4, Claude, etc. (if applicable)

## Context
Background information...

## Prompt
The actual prompt text...

## Expected Output
What should be generated...

## Generated Files
- path/to/file1.ts
- path/to/file2.ts
\\\

---

**Note:** This folder is version controlled. Don't include API keys or sensitive information.
