# Git Conventions

> All agents must follow these conventions when creating commits, branches, or PRs.

---

## Commit Format

Use [Conventional Commits](https://www.conventionalcommits.org/) with scope:

```
type(scope): description
```

### Types

| Type | When to Use |
|------|-------------|
| `feat` | New feature or capability |
| `fix` | Bug fix |
| `refactor` | Code restructuring (no behavior change) |
| `docs` | Documentation only |
| `test` | Adding or updating tests |
| `chore` | Build, config, tooling, dependencies |
| `style` | Formatting, whitespace (no logic change) |
| `perf` | Performance improvement |
| `ci` | CI/CD pipeline changes |

### Scopes

Use the **feature/system name** in lowercase:

```
feat(board): add gem spawning with object pooling
fix(matching): L-shape detection missing diagonal cases
refactor(powerups): extract bomb logic into strategy pattern
docs(gdd): add cascade scoring rules
test(board): add edge-case tests for 8x8 grid boundaries
chore(deps): update DOTween to 1.2.765
```

### Rules
- **Lowercase** everything — type, scope, and description
- **Imperative mood** — "add feature" not "added feature" or "adds feature"
- **No period** at the end
- **50 char limit** for the subject line (soft limit — aim for it)
- **Body** is optional — use for explaining *why*, not *what*

### Multi-line Commits
```
feat(cascade): implement chain detection with combo multiplier

Chains are detected by tracking matched gems between gravity passes.
Each chain level multiplies the score by 1.5x.
Combo counter resets when no new matches are found after a fill.
```

---

## Branch Naming

```
type/scope-short-description
```

### Examples
```
feat/board-gem-spawning
fix/matching-l-shape-detection
refactor/powerups-strategy-pattern
docs/gdd-scoring-update
chore/upgrade-unity-6.2
```

### Rules
- Use **kebab-case** (lowercase, hyphens)
- Keep it short but descriptive
- Prefix with the same types as commits

---

## Branch Strategy (Solo Developer — Trunk-Based)

Dự án được tối ưu hóa cho **Lập trình viên độc lập (Solo Dev)** theo mô hình **Trunk-Based Development** tinh gọn:

| Branch / Tag | Mục đích | Cách sử dụng |
|---|---|---|
| `main` | **Nhánh chính duy nhất** chứa toàn bộ source code | Commit trực tiếp và đẩy code đều đặn |
| `tag: vX.Y.Z` | Đánh dấu các cột mốc playable / nộp bài / release | Gắn tag tại commit ổn định (`git tag v0.1.0-mvp`) |
| `exp/*` | Thử nghiệm mạo hiểm (Optional) | Nhánh tạm khi đập đi xây lại hệ thống lớn, xong thì merge hoặc xóa |

### Quy tắc làm việc nhanh:
1. **Không duy trì nhánh develop**: Toàn bộ quá trình code, refactor và docs diễn ra trên `main`.
2. **Commit nhỏ, rõ ràng**: Mỗi commit giải quyết trọn vẹn một việc theo chuẩn Conventional Commits bên trên.
3. **Đóng mốc bằng Git Tag**:
   ```bash
   # Gắn tag cho cột mốc quan trọng (ví dụ hoàn thành MVP)
   git tag -a v0.1.0-mvp -m "milestone: complete playable mvp"
   git push origin v0.1.0-mvp
   ```

