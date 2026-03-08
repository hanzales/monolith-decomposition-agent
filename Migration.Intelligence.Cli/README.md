# Migration.Intelligence.Cli

Bu CLI aracı, bir monolith kod tabanını analiz ederek mikroservis/parçalama (decomposition) geçişi için teknik çıktı üretir.

## Ne işe yarar?

- Kaynak kodu tarar ve mimari/mantıksal bağımlılıkları çıkarır.
- Domain (bounded context) çıkarımı yapar.
- Migration analizi raporu üretir (`.md` ve `.json`).
- İstenirse domain bazlı migration tasarımı üretir.
- Üretilen tasarımları doğrular.
- Domain artifact dosyaları üretir.
- Deterministic veya LLM destekli agent planı çıkarır.

## Çalışma akışı

1. `--source` ile verilen repo/klasör analiz edilir.
2. Temel analiz raporları `--target` altında oluşturulur.
3. Opsiyonel bayraklara göre:
   - Tasarım çıktıları (`design/`)
   - Doğrulama çıktıları (`validation/`)
   - Agent planı (`agents/`)
   - Domain artifact dosyaları üretilir.

## Gereksinimler

- .NET SDK `10.0` (`net10.0` hedef framework)
- Analiz edilecek kaynak kod dizini

## Temel kullanım

```bash
dotnet run --project Migration.Intelligence.Cli -- --source <kaynak_klasor> --target <cikti_klasoru>
```

Örnek:

```bash
dotnet run --project Migration.Intelligence.Cli -- --source "C:\work\legacy-app" --target "C:\work\migration-output"
```

## Sık kullanılan seçenekler

- `--architecture <md1,md2>`: Mimari doküman(lar)ı ekler.
- `--dry-run`: Yazma/üretim adımlarını sınırlı test etmek için kullanılır.
- `--design-all`: Tüm domainler için migration tasarımı üretir.
- `--design-domain <name1,name2>`: Sadece seçili domain(ler) için tasarım üretir.
- `--validate-design`: Üretilen tasarımları doğrular.
- `--generate-domain-artifacts`: Domain artifact çıktıları üretir.
- `--agent-plan`: Migration öncelik/aksiyon planı üretir.
- `--agent-mode <deterministic|llm>`: Agent çalışma modu.
- `--llm-endpoint <url>`: LLM endpoint override.
- `--llm-model <name>`: LLM model adı.
- `--llm-api-key <key>`: LLM API anahtarı.
- `--llm-timeout-sec <seconds>`: LLM timeout (en az 15).
- `--llm-temperature <0..1>`: LLM temperature.

## LLM modu örneği

```bash
dotnet run --project Migration.Intelligence.Cli -- ^
  --source "C:\work\legacy-app" ^
  --target "C:\work\migration-output" ^
  --agent-plan ^
  --agent-mode llm ^
  --llm-endpoint "https://api.openai.com/v1/chat/completions" ^
  --llm-model "gpt-4.1-mini" ^
  --llm-api-key "<API_KEY>"
```

## Üretilen çıktılar

`--target` altında tipik olarak:

- Migration analiz raporları (`.md`, `.json`)
- `design/` klasörü (domain design çıktıları)
- `validation/` klasörü (portföy doğrulama raporları)
- `agents/` klasörü (migration agent planı)

## Yardım

```bash
dotnet run --project Migration.Intelligence.Cli -- --help
```
