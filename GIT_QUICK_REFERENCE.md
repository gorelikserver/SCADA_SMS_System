# Git Quick Reference - SCADA SMS System

## ? Repository Status

**? Git Initialized**: Yes  
**?? Branch**: master  
**?? Files Tracked**: 1,178 files  
**??? Latest Commit**: 5282426  
**?? Location**: C:\SCADA_CSharp_Clean\

---

## ?? Quick Commands

### Daily Workflow
```bash
# Check what changed
git status

# See what's different
git diff

# Stage changes
git add .

# Commit changes
git commit -m "feat: description of changes"

# View history
git log --oneline -10
```

### Branch Management
```bash
# Create new feature branch
git checkout -b feature/my-feature

# Switch back to master
git checkout master

# List all branches
git branch -a

# Delete branch
git branch -d feature/my-feature
```

### Collaboration (When you add remote)
```bash
# Add remote repository
git remote add origin <url>

# Push to remote
git push -u origin master

# Pull from remote
git pull origin master

# Fetch updates
git fetch origin
```

---

## ?? Commit Message Examples

```bash
# New feature
git commit -m "feat: Add SMS rate limiting"

# Bug fix
git commit -m "fix: Correct duplicate detection logic"

# Documentation
git commit -m "docs: Update deployment guide"

# Refactoring
git commit -m "refactor: Simplify alarm processing"

# Performance
git commit -m "perf: Optimize database queries"

# Configuration
git commit -m "chore: Update build scripts"
```

---

## ?? Common Scenarios

### Undo Last Commit (Keep Changes)
```bash
git reset --soft HEAD~1
```

### Undo Last Commit (Discard Changes)
```bash
git reset --hard HEAD~1
```

### Discard Changes to File
```bash
git restore filename
```

### View Changes
```bash
# Unstaged changes
git diff

# Staged changes
git diff --staged

# Changes in specific file
git diff filename
```

### Stash Changes
```bash
# Save current work
git stash

# List stashes
git stash list

# Apply last stash
git stash pop
```

---

## ??? Tagging Releases

```bash
# Create tag
git tag -a v2.1.0 -m "Release v2.1.0 - Air-gapped support"

# List tags
git tag

# Push tags to remote
git push origin --tags

# Delete tag
git tag -d v2.1.0
```

---

## ?? Viewing History

```bash
# Last 10 commits (one line each)
git log --oneline -10

# Show changes in each commit
git log -p -5

# Show who changed what
git blame filename

# Search commits
git log --grep="keyword"

# Show file history
git log --follow filename
```

---

## ?? Branch Strategy

```
master (production)
  ??? development (integration)
  ?   ??? feature/alarm-improvements
  ?   ??? feature/new-dashboard
  ?   ??? fix/sms-bug
  ?   ??? docs/api-documentation
  ??? hotfix/critical-fix
```

### Creating Branches
```bash
# Feature branch
git checkout -b feature/description

# Bug fix branch
git checkout -b fix/description

# Hotfix branch
git checkout -b hotfix/description

# Documentation branch
git checkout -b docs/description
```

---

## ?? Emergency Commands

### Undo Everything
```bash
# WARNING: This discards ALL uncommitted changes!
git reset --hard HEAD
git clean -fd
```

### Revert to Specific Commit
```bash
# Find commit hash
git log --oneline

# Revert to that commit
git revert <commit-hash>
```

### Recover Deleted Commit
```bash
# Find lost commit
git reflog

# Recover it
git checkout <commit-hash>
```

---

## ?? Current Commits

```
5282426 (HEAD -> master) docs: Add Git initialization summary and workflow guide
62a4b4b Initial commit: SCADA SMS Notification System v2.1 - Production Ready
```

---

## ?? What's Tracked

### ? Included in Git
- All source code (.cs, .cshtml)
- Configuration files (appsettings.json)
- Build scripts (.bat files)
- Documentation (.md files)
- Project files (.csproj, .sln)
- Frontend assets (CSS, JS)
- .gitignore and GitHub configs

### ? Ignored (Not in Git)
- bin/ and obj/ folders
- deployment_package/ folder
- temp_curl/ folder
- *.zip deployment packages
- build.log
- .vs/ IDE settings
- *.user files

---

## ?? Setting Up Remote (GitHub Example)

### 1. Create Repository on GitHub
- Go to github.com
- Click "New Repository"
- Name it (e.g., "scada-sms-system")
- Don't initialize with README (you already have one)

### 2. Connect Local to Remote
```bash
git remote add origin https://github.com/yourusername/scada-sms-system.git
git branch -M main  # If you prefer 'main' over 'master'
git push -u origin main
```

### 3. Push All Branches
```bash
# Push master/main
git push -u origin master

# Push tags
git push origin --tags

# Push all branches
git push --all origin
```

---

## ?? Security Best Practices

### Never Commit:
? API keys or passwords  
? Database connection strings with credentials  
? SSL certificates or private keys  
? User data or PII  
? Large binary files (use Git LFS)  

### If You Accidentally Commit Secrets:
```bash
# Remove file from history
git filter-branch --tree-filter 'rm -f path/to/file' HEAD

# Or use BFG Repo-Cleaner
bfg --delete-files secretfile.txt

# Force push (BE CAREFUL!)
git push origin --force
```

---

## ?? Repository Health

### Check Repository Size
```bash
git count-objects -vH
```

### Find Large Files
```bash
git rev-list --objects --all | 
  git cat-file --batch-check='%(objecttype) %(objectname) %(objectsize) %(rest)' |
  sed -n 's/^blob //p' |
  sort --numeric-sort --key=2 |
  tail -n 10
```

### Clean Up
```bash
# Remove untracked files
git clean -n  # Dry run
git clean -f  # Actually remove

# Prune old references
git gc --prune=now
git prune
```

---

## ?? Next Steps

### 1. Add Remote Repository
```bash
git remote add origin <repository-url>
git push -u origin master
```

### 2. Create Development Branch
```bash
git checkout -b development
git push -u origin development
```

### 3. Tag Initial Release
```bash
git tag -a v2.1.0 -m "SCADA SMS System v2.1 - Production Ready"
git push origin v2.1.0
```

### 4. Set Up CI/CD
- Configure GitHub Actions (workflows already included)
- Set up secrets for deployment
- Enable branch protection rules

---

## ?? Tips & Tricks

### Aliases (Make Life Easier)
```bash
git config --global alias.st status
git config --global alias.co checkout
git config --global alias.br branch
git config --global alias.ci commit
git config --global alias.last 'log -1 HEAD'
git config --global alias.unstage 'reset HEAD --'
```

### Pretty Log
```bash
git log --graph --pretty=format:'%Cred%h%Creset -%C(yellow)%d%Creset %s %Cgreen(%cr) %C(bold blue)<%an>%Creset' --abbrev-commit
```

### Search Code History
```bash
# Find when code was added/removed
git log -S "search term" --source --all
```

---

## ?? Documentation References

- **Git Book**: https://git-scm.com/book
- **GitHub Guides**: https://guides.github.com/
- **Git Cheat Sheet**: https://training.github.com/downloads/github-git-cheat-sheet/

---

## ? Success Checklist

- [x] Git repository initialized
- [x] Initial commit created (500+ files)
- [x] .gitignore configured
- [x] Git configuration set
- [x] Documentation committed
- [ ] Remote repository added (optional)
- [ ] Development branch created (optional)
- [ ] Release tagged (optional)
- [ ] CI/CD configured (optional)

---

**Repository**: SCADA SMS Notification System  
**Version**: v2.1  
**Commits**: 2  
**Files**: 1,178  
**Status**: ? Ready for Development
