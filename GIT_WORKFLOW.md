# Git Workflow: Linear History via Rebasing

This project follows a strict **linear Git history** using rebasing. We do not use merge commits when integrating feature branches into the main branch. This keeps the commit graph perfectly straight, making it much easier to read the history, use `git bisect`, and understand the sequence of changes.

## 1. Setup Your Local Environment
First, configure Git to always rebase when pulling to prevent accidental merge commits from remote updates:
```bash
git config --global pull.rebase true
```

## 2. Starting Work
Always start your work from an up-to-date `main` branch.
```bash
git checkout main
git pull origin main
git checkout -b feature/your-feature-name
```

## 3. Developing and Committing
Work on your feature and commit locally as usual.
```bash
git add .
git commit -m "Add your descriptive commit message"
```

## 4. Syncing with Main (The Most Important Step)
While you are working on your feature, other team members might push changes to `main`. **Do NOT use `git merge main`**. Instead, you must rebase your feature branch on top of the latest `main`.

```bash
# Fetch the latest changes from the remote
git fetch origin

# Rebase your current branch onto the latest main
git rebase origin/main
```

**If there are conflicts during rebase:**
1. Git will pause the rebase.
2. Open your editor and resolve the conflict markers in the affected files.
3. Stage the resolved files: `git add <file>`
4. Continue the rebase: `git rebase --continue`
*(Never run `git commit` during a rebase conflict resolution. Just add and continue).*

## 5. Pushing Your Branch
Because you have rebased, your local branch's history has been rewritten. If you previously pushed this branch to the remote, you must force push:
```bash
git push -u origin feature/your-feature-name --force-with-lease
```
*(Always use `--force-with-lease` rather than `-f` to ensure you don't accidentally overwrite someone else's work on the same branch).*

## 6. Merging into Main
Once your feature is reviewed and ready:

**Option A (If using a platform like GitHub/GitLab):**
Select the **"Squash and Merge"** or **"Rebase and Merge"** option in the Pull Request UI. Do NOT use the standard "Create a merge commit" option.

**Option B (If merging via CLI):**
```bash
git checkout main
git pull origin main
git merge feature/your-feature-name --ff-only
git push origin main
```
*(The `--ff-only` flag guarantees that Git will only merge if it can do a fast-forward, preventing any merge commits. If it fails, it means you need to rebase your feature branch again).*
