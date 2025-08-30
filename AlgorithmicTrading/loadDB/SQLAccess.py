import yfinance as yf
import pymysql

conn = pymysql.connect(
    host='localhost',
    user='root',         # 본인 MySQL 사용자명
    port=3306,
    password='Mm10194105828!21',  # 본인 MySQL 비밀번호
    db="db",
    charset='utf8'
)
cur = conn.cursor()

ticker = input("")
name = input("")
startingDate = input("")
endingDate = input("")

is_intraday = startingDate == endingDate

# list 테이블에 ticker와 name 저장 (없으면 추가)
cur.execute("INSERT IGNORE INTO list (ticker, name) VALUES (%s, %s)", (ticker, name))
conn.commit()
cur.execute("SELECT id FROM list WHERE ticker=%s", (ticker,))
row = cur.fetchone()
if not row:
    print("종목 id를 찾을 수 없습니다.")
    exit()
stock_id = row[0]

if is_intraday:
    # 1분봉 데이터 (입력 날짜의 00:00~23:59)
    date_only = startingDate[:10]
    data = yf.download(ticker, start=date_only, end=date_only, interval="1m")
    if data.empty:
        print("해당 날짜에 데이터가 없습니다.")
    else:
        for idx, row in data.iterrows():
            cur.execute(
                "INSERT INTO data_intraday (date, id, close, high, low, open, volume) VALUES (%s, %s, %s, %s, %s, %s, %s)",
                (str(idx.to_pydatetime()), int(stock_id), float(row['Close'].iloc[0]), float(row['High'].iloc[0]), float(row['Low'].iloc[0]), float(row['Open'].iloc[0]), float(row['Volume'].iloc[0]))
            )
        conn.commit()
        print(f"{name}의 1분봉 데이터가 data_intraday에 저장되었습니다.")
else:
    # 일봉 데이터
    data = yf.download(ticker, start=startingDate, end=endingDate, interval="1d")
    if data.empty:
        print("해당 날짜에 데이터가 없습니다.")
    else:
        for idx, row in data.iterrows():
            
            cur.execute(
                "INSERT INTO data_daily (date, id, close, high, low, open, volume) VALUES (%s, %s, %s, %s,%s, %s, %s)",
                (str(idx.date()), int(stock_id), float(row['Close'].iloc[0]), float(row['High'].iloc[0]), float(row['Low'].iloc[0]), float(row['Open'].iloc[0]), float(row['Volume'].iloc[0]))
            )
        conn.commit()
        print(f"{name}의 일봉 데이터가 data_daily에 저장되었습니다.")

cur.close()
conn.close()