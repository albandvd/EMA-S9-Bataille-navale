# Échanges décisifs avec l'IA

Ce fichier est un livrable noté. Les entrées sont créées automatiquement par le hook
`.claude/hooks/journal-prompt.sh` à chaque prompt envoyé à Claude Code, puis complétées à la
main ou via la commande `/journal`.

Voir `.claude/skills/naval-journal/SKILL.md` pour le format attendu de chaque champ.

**Contrôle avant rendu :**

```bash
grep -c 'TODO' PROMPTS.md                    # doit renvoyer 0
grep -c 'Statut : `brouillon`' PROMPTS.md    # doit renvoyer 0
```
