import tkinter as tk
import time
import math
from datetime import datetime
import requests

# 🌤️ Weerlocatie
weather_city = "Leiden"
weather_country = "NL"

# 🌤️ Weer ophalen
def get_weather():
    try:
        api_key = "653d15bb3beea42def92eda4f833e909"
        url = f"https://api.openweathermap.org/data/2.5/weather?q={weather_city},{weather_country}&appid={api_key}&units=metric&lang=nl"
        data = requests.get(url).json()

        temp = round(data["main"]["temp"])
        icon_code = data["weather"][0]["icon"]

        icon_map = {
            "01d": "☀️", "01n": "🌙",
            "02d": "🌤️", "02n": "☁️",
            "03d": "☁️", "03n": "☁️",
            "04d": "☁️", "04n": "☁️",
            "09d": "🌧️", "09n": "🌧️",
            "10d": "🌦️", "10n": "🌧️",
            "11d": "🌩️", "11n": "🌩️",
            "13d": "❄️", "13n": "❄️",
            "50d": "🌫️", "50n": "🌫️"
        }

        icoon = icon_map.get(icon_code, "🌡️")
        return icoon, f"{temp}°C"
    except:
        return "🌡️", "?°C"


# 📅 Datum in NL
def get_nederlandse_datum():
    maanden = ["Jan", "Feb", "Mrt", "Apr", "Mei", "Jun",
               "Jul", "Aug", "Sep", "Okt", "Nov", "Dec"]
    dagen = ["Ma", "Di", "Wo", "Do", "Vr", "Za", "Zo"]
    v = datetime.now()
    return f"{dagen[v.weekday()]} {v.day} {maanden[v.month - 1]} {v.year}"


# 🕒 Klok instellingen
WIDTH = 420
HEIGHT = 420
CENTER_X = WIDTH // 2
CENTER_Y = HEIGHT // 2
CLOCK_RADIUS = 150

# ⏱️ Timer variabelen
timer_running = False
timer_seconds = 0


# 📁 Positie opslaan / laden
def laad_positie():
    try:
        with open("position.txt", "r") as f:
            x, y = f.read().split(",")
            return int(x), int(y)
    except:
        return None

def sla_positie_op(x, y):
    with open("position.txt", "w") as f:
        f.write(f"{x},{y}")


# 🌤️ Weerinstellingen popup
def weer_instellingen():
    popup = tk.Toplevel(root)
    popup.title("Weerinstellingen")
    popup.geometry("260x180")
    popup.configure(bg="#0a0a23")
    popup.resizable(False, False)

    tk.Label(popup, text="Plaats:", font=("Calibri Semilight", 12),
             fg="#B7BEFF", bg="#0a0a23").place(x=20, y=20)

    tk.Label(popup, text="Landcode (NL, BE, US):", font=("Calibri Semilight", 12),
             fg="#B7BEFF", bg="#0a0a23").place(x=20, y=70)

    city_var = tk.StringVar(value=weather_city)
    country_var = tk.StringVar(value=weather_country)

    tk.Entry(popup, textvariable=city_var, width=15,
             font=("Calibri", 12)).place(x=20, y=45)

    tk.Entry(popup, textvariable=country_var, width=5,
             font=("Calibri", 12)).place(x=20, y=95)

    def opslaan():
        global weather_city, weather_country
        weather_city = city_var.get()
        weather_country = country_var.get()
        popup.destroy()

    tk.Button(popup, text="Opslaan", font=("Calibri Semilight", 12),
              bg="#5050DE", fg="white", bd=0, padx=10, pady=5,
              command=opslaan).place(x=90, y=130)


# ⏱️ Timer instellen popup
def timer_instellen():
    popup = tk.Toplevel(root)
    popup.title("Timer instellen")
    popup.geometry("250x180")
    popup.configure(bg="#0a0a23")
    popup.resizable(False, False)

    tk.Label(popup, text="Uren:", font=("Calibri Semilight", 12), fg="#B7BEFF", bg="#0a0a23").place(x=20, y=20)
    tk.Label(popup, text="Minuten:", font=("Calibri Semilight", 12), fg="#B7BEFF", bg="#0a0a23").place(x=20, y=60)
    tk.Label(popup, text="Seconden:", font=("Calibri Semilight", 12), fg="#B7BEFF", bg="#0a0a23").place(x=20, y=100)

    uren_var = tk.StringVar(value="0")
    min_var = tk.StringVar(value="0")
    sec_var = tk.StringVar(value="0")

    tk.Entry(popup, textvariable=uren_var, width=5, font=("Calibri", 12)).place(x=120, y=20)
    tk.Entry(popup, textvariable=min_var, width=5, font=("Calibri", 12)).place(x=120, y=60)
    tk.Entry(popup, textvariable=sec_var, width=5, font=("Calibri", 12)).place(x=120, y=100)

    def opslaan():
        global timer_seconds
        try:
            h = int(uren_var.get())
            m = int(min_var.get())
            s = int(sec_var.get())
            timer_seconds = h * 3600 + m * 60 + s
        except:
            timer_seconds = 0
        popup.destroy()

    tk.Button(popup, text="Opslaan", font=("Calibri Semilight", 12),
              bg="#5050DE", fg="white", bd=0, padx=10, pady=5,
              command=opslaan).place(x=80, y=140)


# ▶️⏸️🔄 Timer knoppen
def start_timer():
    global timer_running
    timer_running = True

def pause_timer():
    global timer_running
    timer_running = False

def reset_timer():
    global timer_seconds
    timer_seconds = 0


# 🪟 Hoofdvenster
root = tk.Tk()
root.overrideredirect(True)
root.wm_attributes("-transparentcolor", "#0a0a23")

positie = laad_positie()
if positie:
    x, y = positie
    root.geometry(f"{WIDTH}x{HEIGHT}+{x}+{y}")
else:
    root.geometry(f"{WIDTH}x{HEIGHT}+100+100")

canvas = tk.Canvas(root, width=WIDTH, height=HEIGHT, bg="#0a0a23", highlightthickness=0)
canvas.pack()


# 🖱️ Versleepbaar maken
def start_move(event):
    root.x_offset = event.x
    root.y_offset = event.y

def do_move(event):
    x = event.x_root - root.x_offset
    y = event.y_root - root.y_offset
    root.geometry(f"+{x}+{y}")
    sla_positie_op(x, y)


# 🕰️ Klok tekenen
def draw_clock_face():
    for i in range(60):
        angle = math.radians(i * 6)
        x1 = CENTER_X + CLOCK_RADIUS * 0.92 * math.sin(angle)
        y1 = CENTER_Y - CLOCK_RADIUS * 0.92 * math.cos(angle)
        x2 = CENTER_X + CLOCK_RADIUS * math.sin(angle)
        y2 = CENTER_Y - CLOCK_RADIUS * math.cos(angle)
        canvas.create_line(x1, y1, x2, y2, fill="#B7BEFF", width=0.5)

    for i in range(12):
        angle = math.radians(i * 30)
        x1 = CENTER_X + CLOCK_RADIUS * 0.88 * math.sin(angle)
        y1 = CENTER_Y - CLOCK_RADIUS * 0.88 * math.cos(angle)
        x2 = CENTER_X + CLOCK_RADIUS * math.sin(angle)
        y2 = CENTER_Y - CLOCK_RADIUS * math.cos(angle)
        canvas.create_line(x1, y1, x2, y2, fill="#FFFFFF", width=1.5)

        x = CENTER_X + CLOCK_RADIUS * 0.75 * math.sin(angle)
        y = CENTER_Y - CLOCK_RADIUS * 0.75 * math.cos(angle)
        canvas.create_text(x, y, text=str(i if i != 0 else 12),
                           fill="#B7BEFF", font=("Calibri Semilight", 18))

    angle = math.radians(35)
    x = CENTER_X + CLOCK_RADIUS * 1.1 * math.sin(angle)
    y = CENTER_Y - CLOCK_RADIUS * 1.1 * math.cos(angle)
    canvas.create_text(x, y, text="❌", fill="red", font=("Calibri Semilight", 15), tags="close_button")


# 🕹️ Wijzers
def draw_hand(length, angle_deg, color, width):
    angle_rad = math.radians(angle_deg)
    x = CENTER_X + length * math.sin(angle_rad)
    y = CENTER_Y - length * math.cos(angle_rad)
    canvas.create_line(CENTER_X, CENTER_Y, x, y, fill="#FFFFFF", width=width+1, tags="hands")


# 🔄 Update klok
def update_clock():
    global timer_seconds, timer_running

    canvas.delete("hands", "datum", "timer", "timer_label", "weer")

    now = time.localtime()
    sec = now.tm_sec
    minute = now.tm_min
    hour = now.tm_hour % 12

    draw_hand(CLOCK_RADIUS * 0.9, sec * 6, "#ff0000", 1)
    draw_hand(CLOCK_RADIUS * 0.75, minute * 6 + sec * 0.1, "#B7BEFF", 3)
    draw_hand(CLOCK_RADIUS * 0.5, hour * 30 + minute * 0.5, "#B7BEFF", 5)

    canvas.create_oval(CENTER_X - 7, CENTER_Y - 7,
                       CENTER_X + 7, CENTER_Y + 7,
                       fill="#ff0000", tags="hands")

    

    # ⏱️ Aftellen
    if timer_running and timer_seconds > 0:
        timer_seconds -= 1

    if timer_seconds <= 0 and timer_running:
        timer_running = False
        timer_seconds = 0

    h = timer_seconds // 3600
    m = (timer_seconds % 3600) // 60
    s = timer_seconds % 60

# eerste text Timer in de klok face
    canvas.create_text(CENTER_X, HEIGHT - 290,
                       text="Timer",
                       font=("Calibri Semilight", 12),
                       fill="#B7BEFF",
                       tags="timer_label")

#tweede text 00:00:00 in de klok face
    canvas.create_text(CENTER_X, HEIGHT - 260,
                       text=f"{h:02}:{m:02}:{s:02}",
                       font=("Calibri Semilight", 20),
                       fill="#B7BEFF",
                       tags="timer")
#Derde text Datum in de klok face    
    canvas.create_text(CENTER_X, HEIGHT - 230,
                       text=get_nederlandse_datum(),
                       font=("Calibri Semilight", 12),
                       fill="#B7BEFF",
                       tags="datum")

    # 🌤️ Weer opnieuw tekenen
    icoon, temp = get_weather()

    kleur_map = {
        "☀️": "#ffd84d", "🌙": "#ffe680", "🌤️": "#ffe680",
        "☁️": "#d0d0d0", "🌧️": "#66ccff", "🌦️": "#66ccff",
        "🌩️": "#c084ff", "❄️": "#aee6ff", "🌫️": "#bbbbbb"
    }

    kleur = kleur_map.get(icoon, "#ffffff")

    #Vierde text Plaatsnaam in de klok face 
    canvas.create_text(CENTER_X, HEIGHT - 190,
                       text=f"{weather_city}, {weather_country}",
                       font=("Calibri Semilight", 12),
                       fill="#B7BEFF",
                       tags="weer")

    #Vijfde text Icoon in de klok face
    canvas.create_text(CENTER_X, HEIGHT - 160,
                       text=icoon,
                       font=("Segoe UI Emoji", 20),
                       fill=kleur,
                       tags="weer")

    #Zesde text Temeratuur in de clok face 
    canvas.create_text(CENTER_X, HEIGHT - 130,
                       text=temp,
                       font=("Calibri Semilight", 15),
                       fill="#66ccff",
                       tags="weer")

    root.after(1000, update_clock)


# ❌ Sluitknop
def check_close(event):
    items = canvas.find_withtag("close_button")
    for item in items:
        x, y = canvas.coords(item)
        if abs(event.x - x) < 10 and abs(event.y - y) < 10:
            root.destroy()


# 🔗 Binds
canvas.bind("<Button-1>", start_move)
canvas.bind("<B1-Motion>", do_move)
canvas.bind("<ButtonRelease-1>", check_close)


# ▶️⏸️🔄 Knoppen
btn_frame = tk.Frame(root, bg="#B7BEFF")
btn_frame.place(x=CENTER_X - 70, y=HEIGHT - 50)

button_style = {
    "bg": "#5050DE",
    "fg": "white",
    "bd": 0,
    "padx": 5,
    "pady": 2,
    "highlightthickness": 0
}

tk.Button(btn_frame, text="⏱️", command=timer_instellen, **button_style).grid(row=0, column=0, padx=4)
tk.Button(btn_frame, text="▶️", command=start_timer, **button_style).grid(row=0, column=1, padx=4)
tk.Button(btn_frame, text="⏸️", command=pause_timer, **button_style).grid(row=0, column=2, padx=4)
tk.Button(btn_frame, text="🔄", command=reset_timer, **button_style).grid(row=0, column=3, padx=4)
tk.Button(btn_frame, text="🌤️", command=weer_instellingen, **button_style).grid(row=0, column=4, padx=4)


# 🚀 Start
draw_clock_face()
update_clock()
root.mainloop()