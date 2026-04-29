# =========================
# HEAD — CONFIG & SETTINGS
# =========================

import tkinter as tk
import time
import math
from datetime import datetime
import winsound
import requests

# Afmetingen
WIDTH = 420
HEIGHT = 420
CENTER_X = WIDTH // 2
CENTER_Y = HEIGHT // 2
CLOCK_RADIUS = 150

# Weerlocatie
weather_city = "Leiden"
weather_country = "NL"

# Timer
timer_running = False
timer_seconds = 0

# Positie laden / opslaan
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

# Datum in NL
def get_nederlandse_datum():
    maanden = ["Jan","Feb","Mrt","Apr","Mei","Jun","Jul","Aug","Sep","Okt","Nov","Dec"]
    dagen = ["Ma","Di","Wo","Do","Vr","Za","Zo"]
    v = datetime.now()
    return f"{dagen[v.weekday()]} {v.day} {maanden[v.month - 1]} {v.year}"

# Weer ophalen
def get_weather():
    try:
        api_key = "653d15bb3beea42def92eda4f833e909"
        url = f"https://api.openweathermap.org/data/2.5/weather?q={weather_city},{weather_country}&appid={api_key}&units=metric&lang=nl"
        data = requests.get(url).json()

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

        temp = round(data["main"]["temp"])
        icon_code = data["weather"][0]["icon"]
        icoon = icon_map.get(icon_code, "🌡️")
        return icoon, f"{temp}°C"
    except:
        return "🌡️", "?°C"


# =========================
# COMPONENTS — UI ELEMENTS
# =========================
# Tooltip van het menu
def bind_widget_tooltip(widget, text):
    tooltip = tk.Toplevel(widget)
    tooltip.withdraw()
    tooltip.overrideredirect(True)
    tooltip.configure(bg="#222222")

    label = tk.Label(tooltip, text=text, fg="white", bg="#222222",
                     font=("Calibri", 10), padx=5, pady=2)
    label.pack()

    def enter(event):
        x = event.x_root + 10
        y = event.y_root + 10
        tooltip.geometry(f"+{x}+{y}")
        tooltip.deiconify()

    def leave(event):
        tooltip.withdraw()

    widget.bind("<Enter>", enter)
    widget.bind("<Leave>", leave)

# Tooltip van de ovaal
def bind_tooltip(widget, tag, text):
    tooltip = tk.Toplevel(widget)
    tooltip.withdraw()
    tooltip.overrideredirect(True)
    tooltip.configure(bg="#222222")

    label = tk.Label(tooltip, text=text, fg="white", bg="#222222",
                     font=("Calibri", 10), padx=5, pady=2)
    label.pack()

    def enter(event):
        x = event.x_root + 10
        y = event.y_root + 10
        tooltip.geometry(f"+{x}+{y}")
        tooltip.deiconify()

    def leave(event):
        tooltip.withdraw()

    widget.tag_bind(tag, "<Enter>", enter)
    widget.tag_bind(tag, "<Leave>", leave)

# Klok face eerste zijn de seconde
def draw_clock_face():
    for i in range(60):
        angle = math.radians(i * 6)
        x1 = CENTER_X + CLOCK_RADIUS * 0.92 * math.sin(angle)
        y1 = CENTER_Y - CLOCK_RADIUS * 0.92 * math.cos(angle)
        x2 = CENTER_X + CLOCK_RADIUS * math.sin(angle)
        y2 = CENTER_Y - CLOCK_RADIUS * math.cos(angle)
        canvas.create_line(x1, y1, x2, y2, fill="#7B88FC", width=0.5)
# Uur streepjes
    for i in range(12):
        angle = math.radians(i * 30)
        x1 = CENTER_X + CLOCK_RADIUS * 0.88 * math.sin(angle)
        y1 = CENTER_Y - CLOCK_RADIUS * 0.88 * math.cos(angle)
        x2 = CENTER_X + CLOCK_RADIUS * math.sin(angle)
        y2 = CENTER_Y - CLOCK_RADIUS * math.cos(angle)
        canvas.create_line(x1, y1, x2, y2, fill="#B7BEFF", width=1.5)
# Klok cijfers
        x = CENTER_X + CLOCK_RADIUS * 0.75 * math.sin(angle)
        y = CENTER_Y - CLOCK_RADIUS * 0.75 * math.cos(angle)
        canvas.create_text(x, y, text=str(i if i != 0 else 12),
                           fill="#B7BEFF", font=("Calibri Semilight", 18))

# Wijzers
def draw_hand(length, angle_deg, color, width):
    angle_rad = math.radians(angle_deg)
    x = CENTER_X + length * math.sin(angle_rad)
    y = CENTER_Y - length * math.cos(angle_rad)
    canvas.create_line(CENTER_X, CENTER_Y, x, y,
                       fill=color, width=width, tags="hands")
#Timer instelingen menu
def timer_instellen():
    popup = tk.Toplevel(root)
    popup.title("Timer instellen")
    popup.configure(bg="#0a0a23")
    popup.resizable(False, False)

    # Popup onder de klok
    popup.geometry(f"250x180+{x + (WIDTH // 2) - 125}+{y + HEIGHT + 10}")

    tk.Label(popup, text="Uren:", font=("Calibri Semilight", 12),
             fg="#B7BEFF", bg="#0a0a23").place(x=20, y=20)
    tk.Label(popup, text="Minuten:", font=("Calibri Semilight", 12),
             fg="#B7BEFF", bg="#0a0a23").place(x=20, y=60)
    tk.Label(popup, text="Seconden:", font=("Calibri Semilight", 12),
             fg="#B7BEFF", bg="#0a0a23").place(x=20, y=100)

    uren_var = tk.StringVar(value="00")
    min_var = tk.StringVar(value="00")
    sec_var = tk.StringVar(value="00")

    tk.Entry(popup, textvariable=uren_var, width=5,
             font=("Calibri", 12)).place(x=120, y=20)
    tk.Entry(popup, textvariable=min_var, width=5,
             font=("Calibri", 12)).place(x=120, y=60)
    tk.Entry(popup, textvariable=sec_var, width=5,
             font=("Calibri", 12)).place(x=120, y=100)

    def opslaan():
        global timer_seconds, timer_running
        try:
            h = int(uren_var.get())
            m = int(min_var.get())
            s = int(sec_var.get())
            timer_seconds = h * 3600 + m * 60 + s
        except:
            timer_seconds = 0

        timer_running = True
        popup.destroy()

    tk.Button(popup, text="Start Timer", font=("Calibri Semilight", 12),
              bg="#171783", fg="#B7BEFF", bd=1, padx=10, pady=5,
              command=opslaan).place(x=80, y=140)


# Popup: Weer instellen
def weer_instellingen():
    popup = tk.Toplevel(root)
    popup.title("Weerinstellingen")
    popup.configure(bg="#0a0a23")
    popup.resizable(False, False)

    popup.geometry(f"260x180+{x + (WIDTH // 2) - 130}+{y + HEIGHT + 10}")

    tk.Label(popup, text="Plaats:", font=("Calibri Semilight", 12),
             fg="#B7BEFF", bg="#0a0a23").place(x=20, y=20)
    tk.Label(popup, text="Landcode:", font=("Calibri Semilight", 12),
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
              bg="#171783", fg="#B7BEFF", bd=1, padx=10, pady=5,
              command=opslaan).place(x=90, y=130)

# Hamburger-menu
def open_menu():
    menu = tk.Toplevel(root)
    menu.overrideredirect(True)
    menu.configure(bg="#0a0a23")

    menu.geometry(f"160x140+{root.winfo_x() + CENTER_X - 80}+{root.winfo_y() + HEIGHT - 160}")

    def close_menu():
        menu.destroy()

    tk.Button(menu, text="⏱️ Timer instellen", font=("Calibri", 12),
              bg="#0a0a23", fg="#B7BEFF", bd=1,
              command=lambda:[close_menu(), timer_instellen()]).pack(fill="x", pady=3)

    tk.Button(menu, text="🌤️ Weer instellen", font=("Calibri", 12),
              bg="#0a0a23", fg="#B7BEFF", bd=1,
              command=lambda:[close_menu(), weer_instellingen()]).pack(fill="x", pady=3)

    tk.Button(menu, text="❌ Afsluiten", font=("Calibri", 12),
              bg="#0a0a23", fg="#B7BEFF", bd=1,
              command=root.destroy).pack(fill="x", pady=3)


# =========================
# LOGIC — BEHAVIOR
# =========================

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
                       fill="#ff0000", tags=("hands", "center_oval"))

    if timer_running and timer_seconds > 0:
        timer_seconds -= 1
    if timer_seconds <= 0 and timer_running:
        timer_running = False
        timer_seconds = 0
        winsound.PlaySound("Ring05.wav", winsound.SND_FILENAME)


    h = timer_seconds // 3600
    m = (timer_seconds % 3600) // 60
    s = timer_seconds % 60

    canvas.create_text(CENTER_X, HEIGHT - 290, text="Timer",
                       font=("Calibri Semilight", 12),
                       fill="#B7BEFF", tags="timer_label")

    canvas.create_text(CENTER_X, HEIGHT - 260,
                       text=f"{h:02}:{m:02}:{s:02}",
                       font=("Calibri Semilight", 20),
                       fill="#B7BEFF", tags="timer")

    canvas.create_text(CENTER_X, HEIGHT - 230,
                       text=get_nederlandse_datum(),
                       font=("Calibri Semilight", 12),
                       fill="#B7BEFF", tags="datum")

    icoon, temp = get_weather()

    kleur_map = {
        "☀️": "#ffd84d", "🌙": "#ffe680", "🌤️": "#ffe680",
        "☁️": "#d0d0d0", "🌧️": "#66ccff", "🌦️": "#66ccff",
        "🌩️": "#c084ff", "❄️": "#aee6ff", "🌫️": "#bbbbbb"
    }

    kleur = kleur_map.get(icoon, "#ffffff")

    canvas.create_text(CENTER_X, HEIGHT - 190,
                       text=f"{weather_city}, {weather_country}",
                       font=("Calibri Semilight", 12),
                       fill="#B7BEFF", tags="weer")

    canvas.create_text(CENTER_X, HEIGHT - 160,
                       text=icoon,
                       font=("Segoe UI Emoji", 20),
                       fill=kleur, tags="weer")

    canvas.create_text(CENTER_X, HEIGHT - 130,
                       text=temp,
                       font=("Calibri Semilight", 15),
                       fill="#66ccff", tags="weer")

    root.after(1000, update_clock)

# Verslepen
def start_move(event):
    root.x_offset = event.x
    root.y_offset = event.y

def do_move(event):
    x_new = event.x_root - root.x_offset
    y_new = event.y_root - root.y_offset
    root.geometry(f"+{x_new}+{y_new}")
    sla_positie_op(x_new, y_new)


# =========================
# BODY — UI BUILD
# =========================
# Hamburger menu
root = tk.Tk()
root.overrideredirect(True)
root.wm_attributes("-transparentcolor", "#0a0a23")

positie = laad_positie()
if positie:
    x, y = positie
    root.geometry(f"{WIDTH}x{HEIGHT}+{x}+{y}")
else:
    x, y = 100, 100
    root.geometry(f"{WIDTH}x{HEIGHT}+100+100")

canvas = tk.Canvas(root, width=WIDTH, height=HEIGHT, bg="#0a0a23", highlightthickness=0)
canvas.pack()

canvas.bind("<Button-1>", start_move)
canvas.bind("<B1-Motion>", do_move)

btn_frame = tk.Frame(root, bg="#0a0a23")
btn_frame.place(x=CENTER_X - 20, y=HEIGHT - 50)

menu_button = tk.Button(btn_frame, text="☰", font=("Segoe UI Emoji", 18),
                        bg="#0a0a23", fg="#B7BEFF", bd=1,
                        padx=10, pady=2, command=open_menu)
menu_button.grid(row=0, column=0)



# =========================
# SCRIPT — START APP
# =========================

draw_clock_face()
bind_widget_tooltip(menu_button, "Open menu")
root.after(300, lambda: bind_tooltip(canvas, "center_oval", "Klok verslepen"))
update_clock()
root.mainloop()
