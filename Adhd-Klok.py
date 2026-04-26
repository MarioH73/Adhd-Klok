import tkinter as tk
import time
import math
from datetime import datetime
import os

# Afmetingen klok
WIDTH = 420
HEIGHT = 420
CENTER_X = WIDTH // 2
CENTER_Y = HEIGHT // 2
CLOCK_RADIUS = 150

# Timerstatus
timer_running = False
timer_seconds = 0

# 📁 Positie opslaan en laden
def laad_positie():
    try:
        with open("positie.txt", "r") as f:
            x, y = f.read().split(",")
            return int(x), int(y)
    except:
        return None

def sla_positie_op(x, y):
    with open("positie.txt", "w") as f:
        f.write(f"{x},{y}")

# 📅 Datum in het Nederlands
def get_nederlandse_datum():
    maanden = ["Jan", "Feb", "Mrt", "Apr", "Mei", "Jun",
               "Jul", "Aug", "Sep", "Okt", "Nov", "Dec"]
    dagen = ["Ma", "Di", "Wo", "Do", "Vr", "Za", "Zo"]
    vandaag = datetime.now()
    dag = dagen[vandaag.weekday()]
    datum = f"{dag} {vandaag.day} {maanden[vandaag.month - 1]} {vandaag.year}"
    return datum

# ⏱️ Timerfuncties
def start_timer():
    global timer_running
    timer_running = True

def pause_timer():
    global timer_running
    timer_running = False

def reset_timer():
    global timer_seconds
    timer_seconds = 0

# 🪟 Hoofdvenster klok
root = tk.Tk()
root.overrideredirect(True)
root.wm_attributes("-transparentcolor", "#0a0a23")

# 📍 Positie bepalen
screen_width = root.winfo_screenwidth()
screen_height = root.winfo_screenheight()
positie = laad_positie()
if positie:
    window_x, window_y = positie
else:
    window_x = (screen_width // 2) - WIDTH // 2
    window_y = (screen_height // 2) - HEIGHT // 2 + 25
root.geometry(f"{WIDTH}x{HEIGHT}+{window_x}+{window_y}")

canvas = tk.Canvas(root, width=WIDTH, height=HEIGHT, bg="#0a0a23", highlightthickness=0)
canvas.pack()

# 🕰️ Klok tekenen
def draw_clock_face():
    # Minuutstreepjes (gedimd cyaan)
    for i in range(60):
        angle = math.radians(i * 6)
        x_start = CENTER_X + CLOCK_RADIUS * 0.92 * math.sin(angle)
        y_start = CENTER_Y - CLOCK_RADIUS * 0.92 * math.cos(angle)
        x_end = CENTER_X + CLOCK_RADIUS * math.sin(angle)
        y_end = CENTER_Y - CLOCK_RADIUS * math.cos(angle)
        canvas.create_line(x_start, y_start, x_end, y_end, fill="#3399ff", width=0.5)

    # Uurstreepjes (iets langer en dikker)
    for i in range(12):
        angle = math.radians(i * 30)
        x_start = CENTER_X + CLOCK_RADIUS * 0.88 * math.sin(angle)
        y_start = CENTER_Y - CLOCK_RADIUS * 0.88 * math.cos(angle)
        x_end = CENTER_X + CLOCK_RADIUS * math.sin(angle)
        y_end = CENTER_Y - CLOCK_RADIUS * math.cos(angle)
        canvas.create_line(x_start, y_start, x_end, y_end, fill="#FF0000", width=1.5)

    # Uurcijfers
    for i in range(12):
        angle = math.radians(i * 30)
        x = CENTER_X + CLOCK_RADIUS * 0.75 * math.sin(angle)
        y = CENTER_Y - CLOCK_RADIUS * 0.75 * math.cos(angle)
        canvas.create_text(x, y, text=str(i if i != 0 else 12),
                           fill="#B7BEFF", font=("Helvetica", 14, "bold"))

    # ❌ Sluitknop buiten de klokring (ongeveer 35 graden)
    angle = math.radians(35)
    x = CENTER_X + CLOCK_RADIUS * 1.1 * math.sin(angle)
    y = CENTER_Y - CLOCK_RADIUS * 1.1 * math.cos(angle)
    canvas.create_text(x, y, text="❌", fill="red", font=("Helvetica", 10, "bold"), tags="close_button")



# 🕹️ Wijzers tekenen
def draw_hand(length, angle_deg, color, width):
    angle_rad = math.radians(angle_deg)
    x = CENTER_X + length * math.sin(angle_rad)
    y = CENTER_Y - length * math.cos(angle_rad)
    canvas.create_line(CENTER_X, CENTER_Y, x, y, fill=color, width=width, tags="hands")

# 🔄 Klok en timer updaten
def update_clock():
    global timer_seconds, timer_running
    canvas.delete("hands")
    canvas.delete("datum")
    canvas.delete("timer")
    canvas.delete("timer_label")
    canvas.delete("pauze")
    canvas.delete("weer")

    now = time.localtime()
    sec = now.tm_sec
    min = now.tm_min
    hr = now.tm_hour % 12

    sec_angle = sec * 6
    min_angle = min * 6 + sec * 0.1
    hr_angle = hr * 30 + min * 0.5

    draw_hand(CLOCK_RADIUS * 0.9, sec_angle, "#ff0000", 1)
    draw_hand(CLOCK_RADIUS * 0.75, min_angle, "#6663FF", 3)
    draw_hand(CLOCK_RADIUS * 0.5, hr_angle, "#6664fd", 3)

    canvas.create_oval(CENTER_X - 5, CENTER_Y - 5, CENTER_X + 5, CENTER_Y + 5,
                       fill="#ff0000", tags="hands")

    # 📅 Datum onder de klok
    canvas.create_text(CENTER_X, HEIGHT - 150,
                       text=get_nederlandse_datum(),
                       font=("Segoe UI", 11),
                       fill="#fbff00",
                       tags="datum")

    # ⏱️ Timer
    if timer_running:
        timer_seconds += 1

    hours = timer_seconds // 3600
    minutes = (timer_seconds % 3600) // 60
    seconds = timer_seconds % 60
    tijd = f"{hours:02}:{minutes:02}:{seconds:02}"

    # Label boven de tijd
    canvas.create_text(CENTER_X, HEIGHT - 285,
                       text="Timer",
                       font=("Segoe UI", 11, "bold"),
                       fill="#fafffe",
                       tags="timer_label")

    # Tijd zelf
    canvas.create_text(CENTER_X, HEIGHT - 270,
                       text=tijd,
                       font=("Segoe UI", 12, "italic"),
                       fill="#fafffe",
                       tags="timer")

    # ⏸️ Pauzeherinnering na elk uur
    if timer_seconds % 3600 == 0 and timer_seconds != 0:
        canvas.create_text(CENTER_X, HEIGHT - 310,
                           text="Je werkt al een uur. Tijd voor een pauze!",
                           font=("Segoe UI", 10),
                           fill="#ff8080",
                           tags="pauze")
        timer_running = False  # automatisch pauzeren

    # 🌤️ Weerbericht tussen 9 en het middelpunt
    angle = math.radians(255)
    x = CENTER_X + CLOCK_RADIUS * 0.6 * math.sin(angle)
    y = CENTER_Y - CLOCK_RADIUS * 0.6 * math.cos(angle)
    canvas.create_text(x, y,
                       text="🌦️14°C Bewolkt",
                       font=("Segoe UI", 8),
                       fill="#66ccff",
                       tags="weer")

    root.after(1000, update_clock)



    # ⏱️ Timer
    if timer_running:
        timer_seconds += 1

    hours = timer_seconds // 3600
    minutes = (timer_seconds % 3600) // 60
    seconds = timer_seconds % 60
    tijd = f"{hours:02}:{minutes:02}:{seconds:02}"

# Timer label boven de tijd
    canvas.create_text(CENTER_X, HEIGHT - 285,
                       text="Timer",
                       font=("Segoe UI", 11, "bold"),
                       fill="#fafffe",
                       tags="timer_label")

    # Tijd zelf
    canvas.create_text(CENTER_X, HEIGHT - 270,
                       text=tijd,
                       font=("Segoe UI", 12, "italic"),
                       fill="#fafffe",
                       tags="timer")

 # ⏸️ Pauzeherinnering na elk uur
    if timer_seconds % 3600 == 0 and timer_seconds != 0:
        canvas.create_text(CENTER_X, HEIGHT - 310,
                           text="Je werkt al een uur. Tijd voor een pauze!",
                           font=("Helvetica", 10),
                           fill="#ff8080",
                           tags="pauze")
        timer_running = False  # automatisch pauzeren

    root.after(1000, update_clock)

# 🖱️ Versleepbaar maken + positie opslaan
def start_move(event):
    root.x_offset = event.x
    root.y_offset = event.y

def do_move(event):
    x = event.x_root - root.x_offset
    y = event.y_root - root.y_offset
    root.geometry(f"+{x}+{y}")
    sla_positie_op(x, y)

# ❌ Sluitknop detecteren
def check_close(event):
    items = canvas.find_withtag("close_button")
    for item in items:
        coords = canvas.coords(item)
        if len(coords) == 2:
            x, y = coords
            if abs(event.x - x) < 10 and abs(event.y - y) < 10:
                root.destroy()

canvas.bind("<Button-1>", start_move)
canvas.bind("<B1-Motion>", do_move)
canvas.bind("<ButtonRelease-1>", check_close)

# ▶️⏸️🔄 Timerknoppen
btn_frame = tk.Frame(root, bg="#0a0a23")
btn_frame.place(x=CENTER_X - 55, y=HEIGHT - 50)

button_style = {
    "bg": "#050544",
    "fg": "white",
    "bd": 0.5,
    "padx": 0,
    "pady": 0,
    "highlightthickness": 1,
    "highlightbackground": "white"
}

tk.Button(btn_frame, text="▶️", command=start_timer, **button_style).grid(row=0, column=0, padx=3, pady=2)
tk.Button(btn_frame, text="⏸️", command=pause_timer, **button_style).grid(row=0, column=1, padx=3, pady=2)
tk.Button(btn_frame, text="🔄", command=reset_timer, **button_style).grid(row=0, column=2, padx=3, pady=2)

# 🏷️ Bedieningstekst onder knoppen
bediening_label = tk.Label(root, text="Bediening Timer", font=("Helvetica", 10),
                           fg="#5234db", bg="#0a0a23")
bediening_label.place(x=CENTER_X - 60, y=HEIGHT - 25)

# 🚀 Start klok
draw_clock_face()
update_clock()
root.mainloop()