package storage

import (
	"io/fs"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"testing"
)

// Critério da 1.3: o tenant só entra por set_config('app.tenant_id', ..., true).
// SET (ou set_config com false) vale para a conexão e vaza para a próxima
// requisição do pool; SET LOCAL não aceita parâmetro. Varre o código (.go e
// .sql): a documentação cita o SET justamente para proibir.
var tenantSemEscopo = regexp.MustCompile(`(?i)\bSET\s+(SESSION\s+|LOCAL\s+)?app\.tenant_id|set_config\(\s*'app\.tenant_id'[^)]*,\s*false\s*\)`)

func TestSemSetTenantForaDoSetConfigLocal(t *testing.T) {
	err := filepath.WalkDir("../..", func(caminho string, d fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		// Pula node_modules e os diretórios ocultos (.git e afins).
		if d.IsDir() && caminho != "../.." && (d.Name() == "node_modules" || strings.HasPrefix(d.Name(), ".")) {
			return filepath.SkipDir
		}
		if ext := filepath.Ext(caminho); d.IsDir() || (ext != ".go" && ext != ".sql") {
			return nil
		}
		b, err := os.ReadFile(caminho)
		if err != nil {
			return err
		}
		for _, m := range tenantSemEscopo.FindAll(b, -1) {
			t.Errorf("%s: %q; use set_config('app.tenant_id', $1, true)", caminho, m)
		}
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
}
