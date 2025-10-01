# TaskAnalyzerApi (.NET 8 Minimal API)

API này gọi LLM upstream để phân tích câu “giao việc” và trích JSON sạch.

## Chạy nhanh
```bash
dotnet restore
dotnet run
```

Mặc định chạy tại:
- http://localhost:5000
- https://localhost:5001

## Gọi thử
```bash
curl -X POST http://localhost:5000/analyze \
  -H "Content-Type: application/json" \
  -d '{
        "Input": "Tôi muốn giao cho công việc cho ông A làm việc với hóc môn và báo cáo lại cho tôi vào ngày 9/9/2020",
        "Temperature": 0.9
      }'
```

## Ghi chú
- Nếu upstream trả về JSON nằm trong khối ```json ... ```, API sẽ bóc ra và trả về `data` là JSON sạch.
- Nếu không có khối ```json```, API sẽ tìm cặp `{ ... }` đầu tiên.
- Có bước chuẩn hoá smart quotes để tránh lỗi parse.
- Upstream endpoint: `https://ob7hxqlhpj0sc3-8000.proxy.runpod.net/v1/chat/completions`
