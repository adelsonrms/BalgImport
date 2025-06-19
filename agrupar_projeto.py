import os
import re

# Diretórios base
BASE_DIR = os.path.dirname(os.path.abspath(__file__))

# Mapeamento de arquivos por tipo
CS_DIRS = [
    'Controllers', 'Hubs', 'Models', 'Services'
]
JS_DIR = os.path.join('wwwroot', 'js')
VIEWS_DIR = 'Views'

# Arquivos de saída
OUT_CS = 'fonte_csharp.cs'
OUT_JS = 'fonte_js.js'
OUT_VIEWS = 'fonte_views.cshtml'

# Função para agrupar arquivos .cs

def agrupar_cs():
    usings = set()
    blocos = []
    ordem = [
        ('Program.cs', 'Program'),
        ('Models', 'Models'),
        ('Hubs', 'Hubs'),
        ('Services', 'Services'),
        ('Controllers', 'Controllers'),
    ]
    for path, label in ordem:
        if os.path.isfile(os.path.join(BASE_DIR, path)):
            files = [os.path.join(BASE_DIR, path)]
        else:
            dir_path = os.path.join(BASE_DIR, path)
            if not os.path.isdir(dir_path):
                continue
            files = [os.path.join(dir_path, f) for f in os.listdir(dir_path) if f.endswith('.cs')]
        for file in sorted(files):
            with open(file, encoding='utf-8') as f:
                content = f.read()
            # Coleta usings
            for match in re.findall(r'^using [^;]+;', content, re.MULTILINE):
                usings.add(match.strip())
            # Remove usings do bloco
            content_sem_usings = re.sub(r'^using [^;]+;\s*', '', content, flags=re.MULTILINE)
            blocos.append(f'// ========== {os.path.relpath(file, BASE_DIR)} =========='\
                          f'\n{content_sem_usings.strip()}\n')
    # Escreve arquivo final
    with open(os.path.join(BASE_DIR, OUT_CS), 'w', encoding='utf-8') as out:
        out.write('// Usings globais\n')
        for u in sorted(usings):
            out.write(f'{u}\n')
        out.write('\n')
        for bloco in blocos:
            out.write(bloco + '\n')

# Função para agrupar arquivos .js

def agrupar_js():
    js_dir = os.path.join(BASE_DIR, JS_DIR)
    files = [f for f in os.listdir(js_dir) if f.endswith('.js')]
    with open(os.path.join(BASE_DIR, OUT_JS), 'w', encoding='utf-8') as out:
        for file in sorted(files):
            path = os.path.join(js_dir, file)
            with open(path, encoding='utf-8') as f:
                content = f.read()
            out.write(f'// ========== {os.path.relpath(path, BASE_DIR)} ==========\n')
            out.write(content.strip() + '\n\n')

# Função para agrupar arquivos .cshtml

def agrupar_views():
    blocos = []
    for root, dirs, files in os.walk(os.path.join(BASE_DIR, VIEWS_DIR)):
        for file in sorted(files):
            if file.endswith('.cshtml'):
                path = os.path.join(root, file)
                with open(path, encoding='utf-8') as f:
                    content = f.read()
                rel = os.path.relpath(path, BASE_DIR)
                blocos.append(f'@* ========== {rel} ========== *@\n{content.strip()}\n')
    with open(os.path.join(BASE_DIR, OUT_VIEWS), 'w', encoding='utf-8') as out:
        for bloco in blocos:
            out.write(bloco + '\n')

if __name__ == '__main__':
    agrupar_cs()
    agrupar_js()
    agrupar_views()
    print('Arquivos agrupados com sucesso!') 